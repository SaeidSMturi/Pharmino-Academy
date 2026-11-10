using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Web.Services.Content
{
    /// <summary>
    /// Turns an image uploaded from the article editor into a small, web-ready file without visible quality loss.
    ///
    /// • The bytes must decode as a real JPEG, PNG, GIF or WebP (the extension and Content-Type are not trusted;
    ///   SVG is refused because it can carry scripts).
    /// • Larger than <see cref="MaxWidth"/>×<see cref="MaxHeight"/>: scaled down (never up) – still sharp on
    ///   high-DPI screens in an article column.
    /// • Photos → lossy WebP at quality 82 (visually lossless). Screenshots, diagrams and transparent images
    ///   (PNG/GIF/lossless WebP sources) → lossless WebP when it is not much larger, so text and edges stay crisp.
    /// • EXIF/ICC/XMP metadata (camera data, GPS location) is removed; the photo is turned upright first.
    /// • Animated GIFs are kept as they are, so the animation survives.
    /// </summary>
    public sealed class ArticleImageProcessor
    {
        public const int MaxWidth = 1920;
        public const int MaxHeight = 1920;
        private const long MaxPixels = 40_000_000;      // refuses "decompression bomb" images
        private const int MaxDimension = 12_000;
        private const int PhotoQuality = 82;
        private const int GraphicLossyQuality = 90;

        public sealed record Result(byte[] Bytes, string Extension, string ContentType, int Width, int Height);

        public sealed class InvalidImageException : Exception
        {
            public InvalidImageException(string message) : base(message) { }
        }

        public async Task<Result> ProcessAsync(Stream input, CancellationToken ct = default)
        {
            // Work on an in-memory copy (the size was already limited by the caller).
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, ct);
            var original = buffer.ToArray();

            ImageInfo info;
            try
            {
                // Only JPEG/PNG/GIF/WebP decoders exist here, so other formats fail before being parsed.
                info = Image.Identify(SafeImageDecoding.Options(), original);
            }
            catch (Exception)
            {
                throw new InvalidImageException("This file is not a supported image (JPEG, PNG, GIF or WebP).");
            }

            var format = info.Metadata.DecodedImageFormat;
            if (format is not (JpegFormat or PngFormat or GifFormat or WebpFormat))
                throw new InvalidImageException("Only JPEG, PNG, GIF and WebP images can be uploaded.");
            if (info.Width < 1 || info.Height < 1 || info.Width > MaxDimension || info.Height > MaxDimension ||
                (long)info.Width * info.Height > MaxPixels)
                throw new InvalidImageException("The image dimensions are too large.");

            // Animated GIF: keep the original file so the animation is not lost.
            if (format is GifFormat && info.FrameMetadataCollection.Count > 1)
                return new Result(original, ".gif", "image/gif", info.Width, info.Height);

            using var image = Image.Load(SafeImageDecoding.Options(maxFrames: 1), original);
            image.Mutate(x => x.AutoOrient());

            if (image.Width > MaxWidth || image.Height > MaxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxWidth, MaxHeight),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));
            }

            // Drop camera data, GPS location and embedded profiles.
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IptcProfile = null;

            // Lossless-style sources are usually screenshots, diagrams or images with transparency.
            var isGraphic = format is PngFormat or GifFormat ||
                            (format is WebpFormat && info.Metadata.GetWebpMetadata().FileFormat == WebpFileFormatType.Lossless);

            byte[] bytes;
            if (isGraphic)
            {
                var lossless = Encode(image, new WebpEncoder { FileFormat = WebpFileFormatType.Lossless, Quality = 100, Method = WebpEncodingMethod.Level4 });
                var lossy = Encode(image, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = GraphicLossyQuality, Method = WebpEncodingMethod.BestQuality });
                // Pixel-perfect when it costs little; otherwise the high-quality lossy version (photos saved as PNG).
                bytes = lossless.Length <= lossy.Length * 1.25 ? lossless : lossy;
            }
            else
            {
                bytes = Encode(image, new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = PhotoQuality, Method = WebpEncodingMethod.BestQuality });
            }

            return new Result(bytes, ".webp", "image/webp", image.Width, image.Height);
        }

        private static byte[] Encode(Image image, IImageEncoder encoder)
        {
            using var ms = new MemoryStream();
            image.Save(ms, encoder);
            return ms.ToArray();
        }
    }
}
