using SkiaSharp;

namespace Web.Services.Imaging
{
    /// <summary>
    /// <see cref="IImageService"/> on SkiaSharp (MIT licence; Google's Skia engine, maintained by Microsoft).
    /// Linux needs the SkiaSharp.NativeAssets.Linux.NoDependencies package (no system libraries required).
    /// Thread-safe: no shared mutable state; fonts are read-only once loaded.
    /// </summary>
    public sealed class SkiaImageService : IImageService
    {
        // Refuses "decompression bomb" images before any pixel memory is allocated.
        public const int MaxDimension = 12_000;
        public const long MaxPixels = 40_000_000;

        #region Probe

        public ImageProbe Probe(byte[] data)
        {
            var kind = Sniff(data) ?? throw new InvalidImageException("This file is not a supported image (JPEG, PNG, GIF or WebP).");
            using var codec = CreateCodec(data);
            var info = codec.Info;
            CheckSize(info.Width, info.Height);
            return new ImageProbe(kind, info.Width, info.Height, Math.Max(1, codec.FrameCount),
                kind == ImageKind.Webp && IsLosslessWebp(data), HasMetadata(kind, data));
        }

        /// <summary>
        /// True when the file carries data beyond the picture that could identify a person or device:
        /// EXIF (camera, GPS), XMP, text chunks or an embedded colour profile. JPEG is always treated as having it.
        /// </summary>
        private static bool HasMetadata(ImageKind kind, byte[] d) => kind switch
        {
            ImageKind.Png => PngChunks(d).Any(c => c is "eXIf" or "tEXt" or "iTXt" or "zTXt" or "iCCP"),
            ImageKind.Webp => WebpChunks(d).Any(c => c is "EXIF" or "XMP " or "ICCP"),
            ImageKind.Gif => false, // GIF comment/application blocks hold no camera or location data
            _ => true
        };

        /// <summary>PNG: 8-byte signature, then chunks of 4-byte big-endian length + 4-byte type + data + 4-byte CRC.</summary>
        private static IEnumerable<string> PngChunks(byte[] d)
        {
            long pos = 8;
            while (pos + 8 <= d.Length)
            {
                long len = ((long)d[pos] << 24) | ((long)d[pos + 1] << 16) | ((long)d[pos + 2] << 8) | d[pos + 3];
                yield return System.Text.Encoding.ASCII.GetString(d, (int)pos + 4, 4);
                pos += 12 + len;
            }
        }

        /// <summary>WebP: 12-byte RIFF header, then chunks of 4-byte FourCC + 4-byte little-endian size + data (even-padded).</summary>
        private static IEnumerable<string> WebpChunks(byte[] d)
        {
            long pos = 12;
            while (pos + 8 <= d.Length)
            {
                yield return System.Text.Encoding.ASCII.GetString(d, (int)pos, 4);
                long size = BitConverter.ToUInt32(d, (int)pos + 4);
                pos += 8 + size + (size & 1);
            }
        }

        /// <summary>Recognises the four web formats by their file signature only.</summary>
        private static ImageKind? Sniff(byte[] d)
        {
            if (d == null || d.Length < 12) return null;
            if (d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return ImageKind.Jpeg;
            if (d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47 && d[4] == 0x0D && d[5] == 0x0A && d[6] == 0x1A && d[7] == 0x0A) return ImageKind.Png;
            if (d[0] == 'G' && d[1] == 'I' && d[2] == 'F' && d[3] == '8' && (d[4] == '7' || d[4] == '9') && d[5] == 'a') return ImageKind.Gif;
            if (d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return ImageKind.Webp;
            return null;
        }

        /// <summary>True when the WebP's image data is a VP8L (lossless) chunk.</summary>
        private static bool IsLosslessWebp(byte[] d)
        {
            foreach (var c in WebpChunks(d))
            {
                if (c == "VP8L") return true;
                if (c == "VP8 ") return false;
            }
            return false;
        }

        private static SKCodec CreateCodec(byte[] data)
        {
            if (Sniff(data) == null)
                throw new InvalidImageException("This file is not a supported image (JPEG, PNG, GIF or WebP).");
            var codec = SKCodec.Create(new SKMemoryStream(data));
            if (codec == null)
                throw new InvalidImageException("The image is damaged or could not be read.");
            if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Gif or SKEncodedImageFormat.Webp))
            {
                codec.Dispose();
                throw new InvalidImageException("Only JPEG, PNG, GIF and WebP images are supported.");
            }
            return codec;
        }

        private static void CheckSize(int width, int height)
        {
            if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension || (long)width * height > MaxPixels)
                throw new InvalidImageException("The image dimensions are too large.");
        }

        #endregion

        #region WebP

        public EncodedImage Encode(byte[] data, int maxWidth, int maxHeight, ImageEncoding encoding) =>
            EncodeVariants(data, maxWidth, maxHeight, encoding)[0];

        public IReadOnlyList<EncodedImage> EncodeVariants(byte[] data, int maxWidth, int maxHeight, params ImageEncoding[] encodings)
        {
            if (encodings == null || encodings.Length == 0) throw new ArgumentException("At least one encoding is required.", nameof(encodings));

            using var upright = DecodeUpright(data);
            using var scaled = ScaleDown(upright, maxWidth, maxHeight);
            var bitmap = scaled ?? upright;

            using var pixmap = bitmap.PeekPixels();
            var results = new List<EncodedImage>(encodings.Length);
            foreach (var e in encodings)
            {
                using var encoded = (e.Format switch
                {
                    OutputFormat.Png => pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 9)),
                    OutputFormat.WebpLossless => pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, Math.Clamp(e.Quality, 0, 100))),
                    _ => pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Math.Clamp(e.Quality, 0, 100)))
                }) ?? throw new InvalidOperationException("Image encoding failed.");
                results.Add(new EncodedImage(encoded.ToArray(), bitmap.Width, bitmap.Height, e));
            }
            return results;
        }

        /// <summary>Decodes the first frame into premultiplied BGRA and applies the EXIF orientation.</summary>
        private static SKBitmap DecodeUpright(byte[] data)
        {
            using var codec = CreateCodec(data);
            var src = codec.Info;
            CheckSize(src.Width, src.Height);

            var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var decoded = new SKBitmap(info);
            var result = codec.GetPixels(info, decoded.GetPixels());
            if (result != SKCodecResult.Success)
            {
                decoded.Dispose();
                throw new InvalidImageException("The image is damaged or could not be read.");
            }

            var origin = codec.EncodedOrigin;
            if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default) return decoded;

            using (decoded)
            {
                int w = decoded.Width, h = decoded.Height;
                bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
                var upright = new SKBitmap(new SKImageInfo(swap ? h : w, swap ? w : h, SKColorType.Bgra8888, SKAlphaType.Premul));
                // x' = ScaleX*x + SkewX*y + TransX ; y' = SkewY*x + ScaleY*y + TransY
                var m = origin switch
                {
                    SKEncodedOrigin.TopRight => new SKMatrix { ScaleX = -1, TransX = w, ScaleY = 1, Persp2 = 1 },          // mirror
                    SKEncodedOrigin.BottomRight => new SKMatrix { ScaleX = -1, TransX = w, ScaleY = -1, TransY = h, Persp2 = 1 }, // 180°
                    SKEncodedOrigin.BottomLeft => new SKMatrix { ScaleX = 1, ScaleY = -1, TransY = h, Persp2 = 1 },          // flip
                    SKEncodedOrigin.LeftTop => new SKMatrix { SkewX = 1, SkewY = 1, Persp2 = 1 },                              // transpose
                    SKEncodedOrigin.RightTop => new SKMatrix { SkewX = -1, TransX = h, SkewY = 1, Persp2 = 1 },               // 90° clockwise
                    SKEncodedOrigin.RightBottom => new SKMatrix { SkewX = -1, TransX = h, SkewY = -1, TransY = w, Persp2 = 1 }, // transverse
                    SKEncodedOrigin.LeftBottom => new SKMatrix { SkewX = 1, SkewY = -1, TransY = w, Persp2 = 1 },              // 270° clockwise
                    _ => SKMatrix.Identity
                };
                using var canvas = new SKCanvas(upright);
                canvas.Clear(SKColors.Transparent);
                canvas.SetMatrix(m);
                canvas.DrawBitmap(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), null);
                canvas.Flush();
                return upright;
            }
        }

        /// <summary>
        /// Scales down to fit the box keeping the aspect ratio (null when no scaling is needed). Large reductions are
        /// done in halving steps first so fine detail does not alias, then a Mitchell cubic pass for the final size.
        /// </summary>
        private static SKBitmap? ScaleDown(SKBitmap source, int maxWidth, int maxHeight)
        {
            double ratio = 1;
            if (maxWidth > 0) ratio = Math.Min(ratio, (double)maxWidth / source.Width);
            if (maxHeight > 0) ratio = Math.Min(ratio, (double)maxHeight / source.Height);
            if (ratio >= 1) return null;

            int targetW = Math.Max(1, (int)Math.Round(source.Width * ratio));
            int targetH = Math.Max(1, (int)Math.Round(source.Height * ratio));

            SKBitmap current = source;
            while (current.Width / 2 >= targetW * 2 && current.Height / 2 >= targetH * 2)
            {
                var half = current.Resize(new SKSizeI(current.Width / 2, current.Height / 2), new SKSamplingOptions(SKFilterMode.Linear))
                    ?? throw new InvalidOperationException("Image resize failed.");
                if (!ReferenceEquals(current, source)) current.Dispose();
                current = half;
            }

            var result = current.Resize(new SKSizeI(targetW, targetH), new SKSamplingOptions(SKCubicResampler.Mitchell))
                ?? throw new InvalidOperationException("Image resize failed.");
            if (!ReferenceEquals(current, source)) current.Dispose();
            return result;
        }

        #endregion

        #region Compose (PDF pages)

        public byte[] ComposeJpeg(byte[] bgraPixels, int width, int height, int jpegQuality, Action<IImageCanvas> draw)
        {
            if (width < 1 || height < 1 || bgraPixels.Length < (long)width * height * 4)
                throw new ArgumentException("Pixel buffer does not match the size.", nameof(bgraPixels));

            using var page = SKImage.FromPixelCopy(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul), bgraPixels)
                ?? throw new InvalidOperationException("Could not read the page pixels.");
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
                ?? throw new InvalidOperationException("Could not create a drawing surface.");

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.DrawImage(page, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), null);
            draw(new SkiaCanvas(canvas, width, height));
            canvas.Flush();

            using var snapshot = surface.Snapshot();
            using var jpeg = snapshot.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(jpegQuality, 1, 100))
                ?? throw new InvalidOperationException("JPEG encoding failed.");
            return jpeg.ToArray();
        }

        public ImageTypeface LoadTypeface(string fontFilePath)
        {
            var tf = SKTypeface.FromFile(fontFilePath)
                ?? throw new FileNotFoundException("Font could not be loaded.", fontFilePath);
            return new SkiaTypeface(tf);
        }

        private sealed class SkiaTypeface : ImageTypeface
        {
            public SKTypeface Typeface { get; }
            public SkiaTypeface(SKTypeface typeface) => Typeface = typeface;
            public override void Dispose() => Typeface.Dispose();
        }

        private sealed class SkiaCanvas : IImageCanvas
        {
            private readonly SKCanvas _canvas;
            public SkiaCanvas(SKCanvas canvas, int width, int height) { _canvas = canvas; Width = width; Height = height; }
            public int Width { get; }
            public int Height { get; }

            private static SKColor ToSk(ImageColor c) => new(c.R, c.G, c.B, c.A);
            private static SKFont Font(ImageTypeface tf, float size) =>
                new(((SkiaTypeface)tf).Typeface, size) { Edging = SKFontEdging.Antialias, Subpixel = true };

            public void FillRectangle(float x, float y, float width, float height, ImageColor color)
            {
                using var paint = new SKPaint { Color = ToSk(color), IsAntialias = false, Style = SKPaintStyle.Fill };
                _canvas.DrawRect(x, y, width, height, paint);
            }

            public void DrawText(string text, ImageTypeface typeface, float sizePx, ImageColor color, float x, float y)
            {
                using var font = Font(typeface, sizePx);
                using var paint = new SKPaint { Color = ToSk(color), IsAntialias = true };
                // Skia draws from the baseline; callers give the top of the text.
                _canvas.DrawText(text, x, y - font.Metrics.Ascent, SKTextAlign.Left, font, paint);
            }

            public float MeasureTextWidth(string text, ImageTypeface typeface, float sizePx)
            {
                using var font = Font(typeface, sizePx);
                return font.MeasureText(text);
            }

            public void Save() => _canvas.Save();
            public void Restore() => _canvas.Restore();
            public void Translate(float dx, float dy) => _canvas.Translate(dx, dy);
            public void RotateDegrees(float degrees) => _canvas.RotateDegrees(degrees);
        }

        #endregion
    }
}
