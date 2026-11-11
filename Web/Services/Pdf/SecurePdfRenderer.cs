using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Web.Services.Imaging;

namespace Web.Services.Pdf
{
    /// <summary>Who is viewing — used to build the forensic watermark.</summary>
    public sealed record PdfViewer(int UserId, string Email, int PdfId, string IpAddress);

    public interface ISecurePdfRenderer
    {
        /// <summary>Number of pages in the PDF.</summary>
        int GetPageCount(string pdfPath);

        /// <summary>
        /// Renders one page (1-based) to a JPEG with the watermark burned into the pixels.
        /// <paramref name="forPrint"/>: a stronger visible watermark and a "Printed by" footer for paper copies.
        /// </summary>
        byte[] RenderPage(string pdfPath, int pageNumber, PdfViewer viewer, bool forPrint = false);

        /// <summary>Traceable code for this viewer + PDF + day, e.g. PX-416-9F2C1A.</summary>
        string GetForensicCode(PdfViewer viewer);
    }

    /// <summary>
    /// Renders PDF pages server-side with PDFium (Docnet.Core) and burns a per-user forensic
    /// watermark into the image through the site's image module (<see cref="IImageService"/>). The original PDF never leaves the server.
    ///
    /// Each page gets three marks:
    ///  1. a faint diagonal tiled watermark: "PX-416-9F2C1A · ali***@gmail.com"
    ///  2. ~14 near-invisible copies of "PX-416-9F2C1A/u1046/p6/20261019" at positions seeded
    ///     from the code, so a leaked screenshot can be traced even if the visible mark is cropped
    ///     (increase contrast on the leaked image to reveal them)
    ///  3. a small footer: "Licensed to ali***@gmail.com · PX-... · 2026-10-19 07:45 UTC"
    ///
    /// The code is PX-{userId in hex}-{6-char HMAC of userId|pdfId|day}: the user ID can be read
    /// straight from it, and the HMAC part proves it was issued by this server.
    /// </summary>
    public class SecurePdfRenderer : ISecurePdfRenderer
    {
        // ~144 DPI for A4 (595pt * 2 = 1190px wide). Readable and keeps JPEGs around 250-400 KB.
        private const double RenderScale = 2.0;
        // Paper copies: 3x PDF points (~216 DPI on an A4 page) so small print stays sharp on paper.
        private const double PrintRenderScale = 3.0;
        // 88 (not lower): JPEG must keep the faint ~2% watermark so Forensic Reveal can still read it.
        private const int JpegQuality = 88;
        private const int HiddenMarkCount = 14;

        private readonly IMemoryCache _cache;
        private readonly ILogger<SecurePdfRenderer> _logger;
        private readonly byte[] _secret;
        private readonly IImageService _images;
        private readonly ImageTypeface _bold;

        // PDFium is not built for heavy parallel use; also caps CPU per server.
        private static readonly SemaphoreSlim RenderGate = new(Math.Max(1, Environment.ProcessorCount / 2));

        public SecurePdfRenderer(IMemoryCache cache, IConfiguration configuration, IWebHostEnvironment env, ILogger<SecurePdfRenderer> logger, IImageService images)
        {
            _cache = cache;
            _images = images;
            _logger = logger;

            var secret = configuration["PdfProtection:WatermarkSecret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogWarning("PdfProtection:WatermarkSecret is not set; using a built-in fallback. Set a random value in appsettings.json.");
                secret = "Pharmino-PdfWatermark-Fallback-7f3c9e21";
            }
            _secret = Encoding.UTF8.GetBytes(secret);

            // Fonts are bundled with the app (Web/Assets/Fonts) so rendering works on Linux without Arial.
            _bold = images.LoadTypeface(FindFont(env, "DejaVuSans-Bold.ttf"));
        }

        private static string FindFont(IWebHostEnvironment env, string fileName)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", fileName),
                Path.Combine(env.ContentRootPath, "Assets", "Fonts", fileName),
            };
            return candidates.FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException($"Watermark font not found. Expected Web/Assets/Fonts/{fileName} in the published output.");
        }

        #region Public API

        public int GetPageCount(string pdfPath)
        {
            var bytes = GetPdfBytes(pdfPath);
            using var reader = Docnet.Core.DocLib.Instance.GetDocReader(bytes, new Docnet.Core.Models.PageDimensions(1.0));
            return reader.GetPageCount();
        }

        public byte[] RenderPage(string pdfPath, int pageNumber, PdfViewer viewer, bool forPrint = false)
        {
            var bytes = GetPdfBytes(pdfPath);

            RenderGate.Wait();
            try
            {
                using var reader = Docnet.Core.DocLib.Instance.GetDocReader(bytes, new Docnet.Core.Models.PageDimensions(forPrint ? PrintRenderScale : RenderScale));
                if (pageNumber < 1 || pageNumber > reader.GetPageCount())
                    throw new ArgumentOutOfRangeException(nameof(pageNumber));

                byte[] raw;
                int width, height;
                using (var page = reader.GetPageReader(pageNumber - 1))
                {
                    raw = page.GetImage();          // BGRA, transparent background
                    width = page.GetPageWidth();
                    height = page.GetPageHeight();
                }

                var code = GetForensicCode(viewer);
                // White background under the page, watermark on top, JPEG out.
                return _images.ComposeJpeg(raw, width, height, JpegQuality,
                    canvas => ApplyWatermark(canvas, viewer, code, pageNumber, forPrint));
            }
            finally
            {
                RenderGate.Release();
            }
        }

        public string GetForensicCode(PdfViewer viewer)
        {
            var day = DateTime.UtcNow.ToString("yyyyMMdd");
            using var hmac = new HMACSHA256(_secret);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{viewer.UserId}|{viewer.PdfId}|{day}"));
            var uidPart = viewer.UserId.ToString().PadLeft(3, '0');
            var hashPart = Convert.ToHexString(hash, 0, 2); // 4 hex characters
            return $"PX-A{uidPart}{hashPart}".ToUpperInvariant();
        }

        #endregion

        #region Watermark drawing

        private void ApplyWatermark(IImageCanvas canvas, PdfViewer viewer, string code, int pageNumber, bool forPrint)
        {
            int w = canvas.Width, h = canvas.Height;
            var masked = MaskEmail(viewer.Email);
            var now = DateTime.UtcNow;

            // 1) Visible repeating diagonal watermark, alternating rows: code / masked email / PHARMINO.
            float tileSize = Math.Max(12f, w * 0.015f);
            // ~2% darker than white on screen: almost invisible when reading, readable under Forensic Reveal.
            // Same ~2% mark on screen and on paper (owner's choice); the "Printed by" footer stays fully visible.
            var tileColor = new ImageColor(0, 0, 0, 5);
            var textItems = new[] { code, masked, "PHARMINO" };

            // Draw on a square as large as the page diagonal, rotated -28° around the page centre, so the
            // rotated rows still cover every corner.
            float side = (float)Math.Ceiling(Math.Sqrt((double)w * w + (double)h * h));
            float rowGap = Math.Max(55f, w * 0.062f); // dense grid: ~22-26 diagonal bands
            canvas.Save();
            canvas.Translate(w / 2f, h / 2f);
            canvas.RotateDegrees(-28);
            canvas.Translate(-side / 2f, -side / 2f);
            int row = 0;
            for (float y = 0; y < side; y += rowGap, row++)
            {
                var text = textItems[row % 3];
                float colGap = canvas.MeasureTextWidth(text, _bold, tileSize) + (w * 0.09f);
                float offset = (row % 3) * (colGap / 3f);
                for (float x = -offset - colGap; x < side + colGap; x += colGap)
                    canvas.DrawText(text, _bold, tileSize, tileColor, x, y);
            }
            canvas.Restore();

            // 2) Hidden micro-marks: ~2% darker than the page (screen and paper), invisible when reading but strong enough to survive
            //    JPEG compression and screenshots. Forensic Reveal (staff) stretches the near-white tones to show them.
            float hiddenSize = Math.Max(11f, w * 0.0125f);
            var hiddenText = $"{code}/u{viewer.UserId}/p{pageNumber}/{now:yyyyMMdd}";
            var hiddenColor = new ImageColor(0, 0, 0, 5);
            var seed = BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes($"{code}|{pageNumber}")), 0);
            var rng = new Random(seed);
            int maxX = Math.Max(21, w - (int)(w * 0.25));
            int maxY = Math.Max(21, h - 50);
            for (int i = 0; i < HiddenMarkCount; i++)
                canvas.DrawText(hiddenText, _bold, hiddenSize, hiddenColor, rng.Next(20, maxX), rng.Next(20, maxY));

            // 3) Solid navy footer: "Licensed to ali***@gmail.com - PX-A1487197 | PHARMINO ACADEMY ..." and the page number.
            float footerHeight = Math.Max(30f, h * 0.024f);
            var footerText = forPrint
                ? $"Printed by {masked} - {code} - {now:yyyy-MM-dd HH:mm} UTC | PHARMINO ACADEMY - personal copy, do not share"
                : $"Licensed to {masked} - {code} | PHARMINO ACADEMY - GCC HEALTHCARE LICENSING ACADEMY";
            float footerSize = Math.Max(9.5f, w * 0.0102f);
            var footerInk = new ImageColor(220, 230, 245);
            var pageText = pageNumber.ToString("D2");

            canvas.FillRectangle(0, h - footerHeight, w, footerHeight, new ImageColor(8, 20, 48)); // #081430
            float textTop = h - footerHeight * 0.72f;
            canvas.DrawText(footerText, _bold, footerSize, footerInk, w * 0.025f, textTop);
            float pageWidth = canvas.MeasureTextWidth(pageText, _bold, footerSize);
            canvas.DrawText(pageText, _bold, footerSize, footerInk, w - pageWidth - (w * 0.025f), textTop);
        }

        /// <summary>ali.rezaei@gmail.com -> ali***@gmail.com</summary>
        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "unknown user";
            var at = email.IndexOf('@');
            if (at <= 0) return email.Length <= 3 ? email + "***" : email[..3] + "***";
            var local = email[..at];
            return (local.Length <= 3 ? local : local[..3]) + "***" + email[at..];
        }

        #endregion

        #region PDF byte cache

        private byte[] GetPdfBytes(string pdfPath)
        {
            var info = new FileInfo(pdfPath);
            var key = $"securepdf:bytes:{pdfPath}:{info.LastWriteTimeUtc.Ticks}";
            if (_cache.TryGetValue(key, out byte[]? cached) && cached != null) return cached;

            var bytes = File.ReadAllBytes(pdfPath);
            _cache.Set(key, bytes, new MemoryCacheEntryOptions
            {
                Size = bytes.Length, // required: AddMemoryCache has SizeLimit set in Program.cs
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });
            return bytes;
        }

        #endregion
    }
}
