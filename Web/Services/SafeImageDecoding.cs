using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace Web.Services
{
    /// <summary>
    /// Decoder settings for every image the site opens (uploads, covers, the /resize endpoint).
    ///
    /// Only JPEG, PNG, GIF and WebP decoders are registered, so a file in any other format (TIFF, BMP, TGA,
    /// PBM, QOI…) is refused before ImageSharp parses it, whatever its extension. This keeps the known
    /// ImageSharp 3.1.x security issues out of reach: they sit in the TIFF codec (GHSA-v76p-62qx-wwq2,
    /// GHSA-j9gm-c75j-xc9q, GHSA-jjfr-hcj7-qf5w, GHSA-wmxv-xphr-5c9g) and in HistogramEqualization
    /// (GHSA-j3p4-wp97-rph4), which the site never uses. The fixed release (3.2.0) needs a Six Labors
    /// license key to build.
    /// </summary>
    public static class SafeImageDecoding
    {
        public static readonly Configuration Configuration = new(
            new JpegConfigurationModule(),
            new PngConfigurationModule(),
            new GifConfigurationModule(),
            new WebpConfigurationModule());

        /// <summary>Decoder options limited to the four web formats; <paramref name="maxFrames"/> 1 for stills.</summary>
        public static DecoderOptions Options(uint maxFrames = uint.MaxValue) =>
            new() { Configuration = Configuration, MaxFrames = maxFrames };
    }
}
