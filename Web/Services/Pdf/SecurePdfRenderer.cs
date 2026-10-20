using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Web.Services.Pdf
{
    /// <summary>Who is viewing — used to build the forensic watermark.</summary>
    public sealed record PdfViewer(int UserId, string Email, int PdfId, string IpAddress);

    public interface ISecurePdfRenderer
    {
        /// <summary>Number of pages in the PDF.</summary>
        int GetPageCount(string pdfPath);

        /// <summary>Renders one page (1-based) to a JPEG with the watermark burned into the pixels.</summary>
        byte[] RenderPage(string pdfPath, int pageNumber, PdfViewer viewer);

        /// <summary>Traceable code for this viewer + PDF + day, e.g. PX-416-9F2C1A.</summary>
        string GetForensicCode(PdfViewer viewer);
    }

    /// <summary>
    /// Renders PDF pages server-side with PDFium (Docnet.Core) and burns a per-user forensic
    /// watermark into the image with ImageSharp. The original PDF never leaves the server.
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
        private const int JpegQuality = 82;
        private const int HiddenMarkCount = 14;

        private readonly IMemoryCache _cache;
        private readonly ILogger<SecurePdfRenderer> _logger;
        private readonly byte[] _secret;
        private readonly FontFamily _boldFamily;
        private readonly FontFamily _monoFamily;

        // PDFium is not built for heavy parallel use; also caps CPU per server.
        private static readonly SemaphoreSlim RenderGate = new(Math.Max(1, Environment.ProcessorCount / 2));

        public SecurePdfRenderer(IMemoryCache cache, IConfiguration configuration, IWebHostEnvironment env, ILogger<SecurePdfRenderer> logger)
        {
            _cache = cache;
            _logger = logger;

            var secret = configuration["PdfProtection:WatermarkSecret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogWarning("PdfProtection:WatermarkSecret is not set; using a built-in fallback. Set a random value in appsettings.json.");
                secret = "Pharmino-PdfWatermark-Fallback-7f3c9e21";
            }
            _secret = Encoding.UTF8.GetBytes(secret);

            // Fonts are bundled with the app (Web/Assets/Fonts) so rendering works on Linux without Arial.
            var fonts = new FontCollection();
            _boldFamily = fonts.Add(FindFont(env, "DejaVuSans-Bold.ttf"));
            _monoFamily = fonts.Add(FindFont(env, "DejaVuSansMono.ttf"));
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

        public byte[] RenderPage(string pdfPath, int pageNumber, PdfViewer viewer)
        {
            var bytes = GetPdfBytes(pdfPath);

            RenderGate.Wait();
            try
            {
                using var reader = Docnet.Core.DocLib.Instance.GetDocReader(bytes, new Docnet.Core.Models.PageDimensions(RenderScale));
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

                using var image = Image.LoadPixelData<Bgra32>(raw, width, height);
                image.Mutate(ctx => ctx.BackgroundColor(Color.White));

                var code = GetForensicCode(viewer);
                ApplyWatermark(image, viewer, code, pageNumber);

                using var output = new MemoryStream();
                image.SaveAsJpeg(output, new JpegEncoder { Quality = JpegQuality });
                return output.ToArray();
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
            return $"PX-{viewer.UserId:X}-{Convert.ToHexString(hash, 0, 3)}";
        }

        #endregion

        #region Watermark drawing

        private void ApplyWatermark(Image<Bgra32> image, PdfViewer viewer, string code, int pageNumber)
        {
            int w = image.Width, h = image.Height;
            var masked = MaskEmail(viewer.Email);
            var now = DateTime.UtcNow;

            // 1) Visible tiled diagonal watermark (drawn on a larger layer, rotated, then cropped).
            var tileFont = _boldFamily.CreateFont(Math.Max(14f, w * 0.022f), FontStyle.Bold);
            var tileText = $"{code}  ·  {masked}";
            var tileColor = Color.FromRgba(30, 40, 90, 34);
            var textWidth = TextMeasurer.MeasureSize(tileText, new TextOptions(tileFont)).Width;

            int side = (int)Math.Ceiling(Math.Sqrt((double)w * w + (double)h * h));
            using (var layer = new Image<Rgba32>(side, side))
            {
                float rowGap = w * 0.16f;
                float colGap = textWidth + w * 0.08f;
                layer.Mutate(ctx =>
                {
                    int row = 0;
                    for (float y = 0; y < side; y += rowGap, row++)
                    {
                        float offset = (row % 2) * (colGap / 2f);
                        for (float x = -offset; x < side; x += colGap)
                            ctx.DrawText(tileText, tileFont, tileColor, new PointF(x, y));
                    }
                    ctx.Rotate(-30);
                });

                var cropX = (layer.Width - w) / 2;
                var cropY = (layer.Height - h) / 2;
                layer.Mutate(ctx => ctx.Crop(new Rectangle(cropX, cropY, w, h)));
                image.Mutate(ctx => ctx.DrawImage(layer, new Point(0, 0), 1f));
            }

            // 2) Hidden forensic marks: alpha ~2%, invisible at normal viewing,
            //    positions differ per user and per page.
            var hiddenFont = _monoFamily.CreateFont(Math.Max(10f, w * 0.013f), FontStyle.Regular);
            var hiddenText = $"{code}/u{viewer.UserId}/p{pageNumber}/{now:yyyyMMdd}";
            var hiddenColor = Color.FromRgba(0, 0, 0, 6);
            var seed = BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes($"{code}|{pageNumber}")), 0);
            var rng = new Random(seed);
            int maxX = Math.Max(1, w - (int)(w * 0.25));
            int maxY = Math.Max(1, h - 40);
            image.Mutate(ctx =>
            {
                for (int i = 0; i < HiddenMarkCount; i++)
                    ctx.DrawText(hiddenText, hiddenFont, hiddenColor, new PointF(rng.Next(20, maxX), rng.Next(20, maxY)));
            });

            // 3) Footer stamp.
            var footerFont = _monoFamily.CreateFont(Math.Max(10f, w * 0.011f), FontStyle.Regular);
            var footer = $"Licensed to {masked} · {code} · {now:yyyy-MM-dd HH:mm} UTC";
            image.Mutate(ctx => ctx.DrawText(footer, footerFont, Color.FromRgba(110, 110, 110, 230),
                new PointF(w * 0.03f, h - w * 0.028f)));
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
