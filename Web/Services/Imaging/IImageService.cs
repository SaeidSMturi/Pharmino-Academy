namespace Web.Services.Imaging
{
    /// <summary>
    /// The site's single entry point for image work: checking uploads, resizing/compressing to WebP and drawing the
    /// burned-in PDF watermarks. Everything else in the site talks to this interface only, so the image library
    /// behind it (today SkiaSharp, MIT licence: <see cref="SkiaImageService"/>) can be swapped without touching callers.
    ///
    /// Security: only JPEG, PNG, GIF and WebP are accepted. The file signature is checked before the library parses
    /// anything, so other formats (TIFF, BMP, SVG, HEIC, ICO…) are refused whatever their extension or Content-Type.
    /// </summary>
    public interface IImageService
    {
        /// <summary>Reads format, size and frame count without decoding pixels. Throws <see cref="InvalidImageException"/>.</summary>
        ImageProbe Probe(byte[] data);

        /// <summary>
        /// Decodes the first frame, turns it upright (EXIF orientation), scales it down to fit
        /// <paramref name="maxWidth"/>×<paramref name="maxHeight"/> (0 = no limit; never scales up) and encodes it.
        /// Metadata (EXIF/GPS, ICC, XMP) is never written to the output. Throws <see cref="InvalidImageException"/>.
        /// </summary>
        EncodedImage Encode(byte[] data, int maxWidth, int maxHeight, ImageEncoding encoding);

        /// <summary>As <see cref="Encode"/>, but decodes once and returns one result per encoding (same order).</summary>
        IReadOnlyList<EncodedImage> EncodeVariants(byte[] data, int maxWidth, int maxHeight, params ImageEncoding[] encodings);

        /// <summary>
        /// Puts raw BGRA pixels (unpremultiplied, e.g. a rendered PDF page) on a white background, lets
        /// <paramref name="draw"/> paint on top, and encodes the result as JPEG.
        /// </summary>
        byte[] ComposeJpeg(byte[] bgraPixels, int width, int height, int jpegQuality, Action<IImageCanvas> draw);

        /// <summary>Loads a TrueType/OpenType font file for <see cref="IImageCanvas.DrawText"/>. Load once and reuse.</summary>
        ImageTypeface LoadTypeface(string fontFilePath);
    }

    /// <summary>Drawing surface handed to <see cref="IImageService.ComposeJpeg"/>. Coordinates are pixels, origin top-left.</summary>
    public interface IImageCanvas
    {
        int Width { get; }
        int Height { get; }

        void FillRectangle(float x, float y, float width, float height, ImageColor color);

        /// <summary>Draws one line of text; (<paramref name="x"/>, <paramref name="y"/>) is the top-left of the text.</summary>
        void DrawText(string text, ImageTypeface typeface, float sizePx, ImageColor color, float x, float y);

        float MeasureTextWidth(string text, ImageTypeface typeface, float sizePx);

        /// <summary>Saves the current transform; pair with <see cref="Restore"/>.</summary>
        void Save();
        void Restore();
        void Translate(float dx, float dy);
        /// <summary>Rotates later drawing; negative = counter-clockwise.</summary>
        void RotateDegrees(float degrees);
    }

    public enum ImageKind { Jpeg, Png, Gif, Webp }

    /// <param name="HasMetadata">EXIF/GPS, XMP, text chunks or a colour profile present (always true for JPEG).</param>
    public sealed record ImageProbe(ImageKind Kind, int Width, int Height, int FrameCount, bool IsLosslessWebp, bool HasMetadata)
    {
        public string ContentType => Kind switch
        {
            ImageKind.Jpeg => "image/jpeg",
            ImageKind.Png => "image/png",
            ImageKind.Gif => "image/gif",
            _ => "image/webp"
        };
    }

    public enum OutputFormat { WebpLossy, WebpLossless, Png }

    /// <summary>
    /// WebpLossy: <paramref name="Quality"/> 0–100 (75–82 is visually lossless for photos).
    /// WebpLossless: <paramref name="Quality"/> is compression effort 0–100. Png: lossless, quality ignored.
    /// </summary>
    public sealed record ImageEncoding(OutputFormat Format, int Quality = 100)
    {
        public static ImageEncoding WebpLossy(int quality) => new(OutputFormat.WebpLossy, quality);
        public static ImageEncoding WebpLossless(int effort = 100) => new(OutputFormat.WebpLossless, effort);
        public static ImageEncoding Png() => new(OutputFormat.Png);

        public string Extension => Format == OutputFormat.Png ? ".png" : ".webp";
        public string ContentType => Format == OutputFormat.Png ? "image/png" : "image/webp";
    }

    public sealed record EncodedImage(byte[] Bytes, int Width, int Height, ImageEncoding Encoding)
    {
        public bool IsLossless => Encoding.Format != OutputFormat.WebpLossy;
    }

    public readonly record struct ImageColor(byte R, byte G, byte B, byte A = 255);

    /// <summary>A loaded font; the concrete type belongs to the image library behind <see cref="IImageService"/>.</summary>
    public abstract class ImageTypeface : IDisposable
    {
        public abstract void Dispose();
    }

    /// <summary>The bytes are not a JPEG/PNG/GIF/WebP image the site can use (or are damaged / too large).</summary>
    public sealed class InvalidImageException : Exception
    {
        public InvalidImageException(string message) : base(message) { }
    }
}
