using Web.Services.Imaging;

namespace Web.Services.Content
{
    /// <summary>
    /// Turns an image uploaded from the article editor into a small, web-ready file without visible quality loss.
    ///
    /// • The bytes must be a real JPEG, PNG, GIF or WebP (the extension and Content-Type are not trusted;
    ///   SVG is refused because it can carry scripts). Checked by the image module (<see cref="IImageService"/>).
    /// • Larger than <see cref="MaxWidth"/>×<see cref="MaxHeight"/>: scaled down (never up) – still sharp on
    ///   high-DPI screens in an article column.
    /// • Photos → lossy WebP at quality 80 (visually lossless). Screenshots, diagrams and transparent images
    ///   (PNG/GIF/lossless WebP sources) → the smaller of lossless WebP and PNG when it is not much larger than a
    ///   high-quality lossy WebP, so text and edges stay crisp; otherwise that lossy WebP.
    /// • EXIF/ICC/XMP metadata (camera data, GPS location) is removed; the photo is turned upright first.
    /// • Animated GIFs are kept as they are, so the animation survives.
    /// </summary>
    public sealed class ArticleImageProcessor
    {
        public const int MaxWidth = 1920;
        public const int MaxHeight = 1920;
        private const int PhotoQuality = 80;
        private const int GraphicLossyQuality = 90;

        private readonly IImageService _images;

        public ArticleImageProcessor(IImageService images) => _images = images;

        public sealed record Result(byte[] Bytes, string Extension, string ContentType, int Width, int Height);

        public async Task<Result> ProcessAsync(Stream input, CancellationToken ct = default)
        {
            // Work on an in-memory copy (the size was already limited by the caller).
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, ct);
            var original = buffer.ToArray();

            // Throws InvalidImageException for anything that is not a usable JPEG/PNG/GIF/WebP.
            var probe = _images.Probe(original);

            // Animated GIF: keep the original file so the animation is not lost.
            if (probe.Kind == ImageKind.Gif && probe.FrameCount > 1)
                return new Result(original, ".gif", "image/gif", probe.Width, probe.Height);

            // Lossless-style sources are usually screenshots, diagrams or images with transparency.
            var isGraphic = probe.Kind is ImageKind.Png or ImageKind.Gif || (probe.Kind == ImageKind.Webp && probe.IsLosslessWebp);

            EncodedImage chosen;
            if (isGraphic)
            {
                var v = _images.EncodeVariants(original, MaxWidth, MaxHeight,
                    ImageEncoding.WebpLossless(), ImageEncoding.Png(), ImageEncoding.WebpLossy(GraphicLossyQuality));
                var lossless = v[0].Bytes.Length <= v[1].Bytes.Length ? v[0] : v[1];
                // Pixel-perfect when it costs little; otherwise the high-quality lossy version (photos saved as PNG).
                chosen = lossless.Bytes.Length <= v[2].Bytes.Length * 1.25 ? lossless : v[2];

                // Never make a clean, already-optimised file bigger: keep the original PNG/WebP when it is smaller,
                // fits the size limit and carries no metadata. (It has just been fully decoded, so it is a valid image.)
                if (probe.Kind is ImageKind.Png or ImageKind.Webp && !probe.HasMetadata && probe.FrameCount == 1 &&
                    probe.Width <= MaxWidth && probe.Height <= MaxHeight && original.Length < chosen.Bytes.Length)
                {
                    var ext = probe.Kind == ImageKind.Png ? ".png" : ".webp";
                    return new Result(original, ext, probe.ContentType, probe.Width, probe.Height);
                }
            }
            else
            {
                chosen = _images.Encode(original, MaxWidth, MaxHeight, ImageEncoding.WebpLossy(PhotoQuality));
            }

            return new Result(chosen.Bytes, chosen.Encoding.Extension, chosen.Encoding.ContentType, chosen.Width, chosen.Height);
        }
    }
}
