using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class HlsProcessingService : IHlsProcessingService
    {
        private readonly string _ffmpegPath;
        private readonly ILogger<HlsProcessingService> _logger;
        private readonly IStorageService _storageService;

        public const string PlaylistFileName = "index.m3u8";
        public const string KeyFileName = "enc.key";
        public const string KeyInfoFileName = "enc.keyinfo";
        public const string SegmentPrefix = "seg";
        public const string SegmentExtension = ".ts";
        public const string KeyUriPlaceholder = "KEYHOLDER_PLACEHOLDER";

        public HlsProcessingService(
            IConfiguration configuration,
            ILogger<HlsProcessingService> logger,
            IStorageService storageService)
        {
            _logger = logger;
            _storageService = storageService;
            _ffmpegPath = configuration["HLS:FfmpegPath"] ?? "ffmpeg";
        }

        public string GetHlsDirectory(int courseId, string videoGuid) =>
            $"uploads/courses/{courseId}/hls/{videoGuid}";

        public async Task<bool> HlsExistsAsync(int courseId, string videoGuid) =>
            await _storageService.FileExistsAsync(GetHlsDirectory(courseId, videoGuid), PlaylistFileName);

        public async Task<string> ReadPlaylistAsync(int courseId, string videoGuid) =>
            await _storageService.ReadFileTextAsync(GetHlsDirectory(courseId, videoGuid), PlaylistFileName);

        public async Task<string> ReadVariantPlaylistAsync(int courseId, string videoGuid, string quality) =>
            await _storageService.ReadFileTextAsync(GetHlsDirectory(courseId, videoGuid), $"{quality}.m3u8");

        public async Task<byte[]> ReadKeyAsync(int courseId, string videoGuid) =>
            await _storageService.ReadFileBytesAsync(GetHlsDirectory(courseId, videoGuid), KeyFileName);

        public async Task<byte[]> ReadSegmentAsync(int courseId, string videoGuid, int segIndex, string? quality = null, string? variant = null)
        {
            string dir = GetHlsDirectory(courseId, videoGuid);
            var isVariantB = string.Equals(variant, "b", StringComparison.OrdinalIgnoreCase);

            if (isVariantB)
            {
                if (!string.IsNullOrEmpty(quality))
                {
                    string bQuality = $"{SegmentPrefix}_{quality}_{segIndex:D3}_b{SegmentExtension}";
                    if (await _storageService.FileExistsAsync(dir, bQuality))
                        return await _storageService.ReadFileBytesAsync(dir, bQuality);
                }
                string bFile = $"{SegmentPrefix}{segIndex:D3}_b{SegmentExtension}";
                if (await _storageService.FileExistsAsync(dir, bFile))
                    return await _storageService.ReadFileBytesAsync(dir, bFile);
                string bAltFile = $"{SegmentPrefix}_{segIndex:D3}_b{SegmentExtension}";
                if (await _storageService.FileExistsAsync(dir, bAltFile))
                    return await _storageService.ReadFileBytesAsync(dir, bAltFile);
            }

            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"{SegmentPrefix}_{quality}_{segIndex:D3}{SegmentExtension}";
                if (await _storageService.FileExistsAsync(dir, qualityFileName))
                    return await _storageService.ReadFileBytesAsync(dir, qualityFileName);
            }
            string fallbackFileName = $"{SegmentPrefix}{segIndex:D3}{SegmentExtension}";
            if (await _storageService.FileExistsAsync(dir, fallbackFileName))
                return await _storageService.ReadFileBytesAsync(dir, fallbackFileName);
            return await _storageService.ReadFileBytesAsync(dir, $"{SegmentPrefix}_{segIndex:D3}{SegmentExtension}");
        }

        public async Task<(Stream fileStream, long contentLength)> GetSegmentStreamAsync(int courseId, string videoGuid, int segmentIndex, string? quality = null, string? variant = null)
        {
            string directoryPath = GetHlsDirectory(courseId, videoGuid);
            var isVariantB = string.Equals(variant, "b", StringComparison.OrdinalIgnoreCase);

            if (isVariantB)
            {
                if (!string.IsNullOrEmpty(quality))
                {
                    string bQuality = $"{SegmentPrefix}_{quality}_{segmentIndex:D3}_b{SegmentExtension}";
                    try { return await _storageService.GetFileStreamAsync(directoryPath, bQuality); }
                    catch (FileNotFoundException) { }
                }
                string bFile = $"{SegmentPrefix}{segmentIndex:D3}_b{SegmentExtension}";
                try { return await _storageService.GetFileStreamAsync(directoryPath, bFile); }
                catch (FileNotFoundException) { }
                string bAltFile = $"{SegmentPrefix}_{segmentIndex:D3}_b{SegmentExtension}";
                try { return await _storageService.GetFileStreamAsync(directoryPath, bAltFile); }
                catch (FileNotFoundException) { }
            }

            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"{SegmentPrefix}_{quality}_{segmentIndex:D3}{SegmentExtension}";
                try
                {
                    return await _storageService.GetFileStreamAsync(directoryPath, qualityFileName);
                }
                catch (FileNotFoundException)
                {
                    // Fallback to single-quality filename if variant not found
                }
            }
            string fallbackFileName = $"{SegmentPrefix}{segmentIndex:D3}{SegmentExtension}";
            try
            {
                return await _storageService.GetFileStreamAsync(directoryPath, fallbackFileName);
            }
            catch (FileNotFoundException)
            {
                string altFileName = $"{SegmentPrefix}_{segmentIndex:D3}{SegmentExtension}";
                return await _storageService.GetFileStreamAsync(directoryPath, altFileName);
            }
        }

        public async Task<int> GetSegmentCountAsync(int courseId, string videoGuid)
        {
            if (!await HlsExistsAsync(courseId, videoGuid)) return 0;
            var playlist = await ReadPlaylistAsync(courseId, videoGuid);
            return Regex.Matches(playlist, @"#EXTINF:").Count;
        }

        public string GetKeyPath(int courseId, string videoGuid) =>
            $"{GetHlsDirectory(courseId, videoGuid)}/{KeyFileName}";

        public string GetSegmentPath(int courseId, string videoGuid, int segIndex, string? quality = null, string? variant = null)
        {
            var suffix = string.Equals(variant, "b", StringComparison.OrdinalIgnoreCase) ? "_b" : "";
            if (!string.IsNullOrEmpty(quality))
                return $"{GetHlsDirectory(courseId, videoGuid)}/{SegmentPrefix}_{quality}_{segIndex:D3}{suffix}{SegmentExtension}";
            return $"{GetHlsDirectory(courseId, videoGuid)}/{SegmentPrefix}{segIndex:D3}{suffix}{SegmentExtension}";
        }

        public async Task<HlsProcessingResult> ProcessVideoAsync(
            int courseId, string inputFilePath, string videoGuid,
            bool multiQuality = false,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"hls_{videoGuid}");
            Directory.CreateDirectory(tempDir);

            var finalStorageDir = GetHlsDirectory(courseId, videoGuid);

            try
            {
                var keyBytes = RandomNumberGenerator.GetBytes(16);
                var tempKeyPath = Path.Combine(tempDir, KeyFileName);
                await File.WriteAllBytesAsync(tempKeyPath, keyBytes, cancellationToken);

                var ivHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                var keyInfoPath = Path.Combine(tempDir, KeyInfoFileName);
                var keyInfoLines = new[] { KeyUriPlaceholder, tempKeyPath, ivHex };
                await File.WriteAllLinesAsync(keyInfoPath, keyInfoLines, cancellationToken);

                var tempPlaylistPath = Path.Combine(tempDir, PlaylistFileName);

                var ffmpegArgs = BuildFfmpegArgs(inputFilePath, tempDir, keyInfoPath, multiQuality);
                _logger.LogInformation("🎬 Starting HLS FFmpeg (Mode: {Mode}, hls_time: 10s) — CourseId: {CourseId}, VideoGuid: {Guid}",
                    multiQuality ? "Multi-Quality (1080p+720p+480p)" : "Fast Smart Copy", courseId, videoGuid);

                var runResult = await RunFfmpegAsync(ffmpegArgs, progress, cancellationToken);
                if (!runResult.Success) return runResult;

                if (!File.Exists(tempPlaylistPath))
                    return new HlsProcessingResult { Success = false, Error = "Playlist not generated" };

                // 🔬 فاز ۲: تولید چانک‌های واریانت B (Forensic Watermarking) برای سگمنت‌های کد
                await GenerateVariantBSegmentsAsync(tempDir, keyBytes, ivHex, cancellationToken);

                _logger.LogInformation("🚀 FFmpeg finished. Uploading HLS segments to Storage...");

                var filesToUpload = Directory.GetFiles(tempDir)
                    .Where(f => !f.EndsWith(KeyInfoFileName))
                    .ToList();

                await _storageService.UploadMultiplePhysicalFilesAsync(filesToUpload, finalStorageDir);

                var segmentCount = filesToUpload.Count(f => f.EndsWith(SegmentExtension));

                _logger.LogInformation("✅ HLS uploaded successfully — Total Segments & Playlists: {Count}", filesToUpload.Count);

                return new HlsProcessingResult
                {
                    Success = true,
                    PlaylistVirtualDirectory = finalStorageDir,
                    SegmentCount = segmentCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ HLS processing failed — CourseId: {CourseId}, VideoGuid: {Guid}", courseId, videoGuid);
                return new HlsProcessingResult { Success = false, Error = ex.Message };
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); }
                catch { /* Ignore */ }
            }
        }

        private static string BuildFfmpegArgs(string inputPath, string tempDir, string keyInfoPath, bool multiQuality)
        {
            var cleanTempDir = tempDir.Replace("\\", "/");
            var cleanKeyInfo = keyInfoPath.Replace("\\", "/");

            if (!multiQuality)
            {
                // ⚡ Fast Smart Copy mode: No re-encoding video frames, AES-128 encryption + 10s segments
                return string.Join(" ",
                    "-y",
                    $"-i \"{inputPath}\"",
                    "-c:v copy",
                    "-c:a aac -b:a 128k",
                    "-f hls",
                    "-hls_time 10",
                    "-hls_list_size 0",
                    $"-hls_key_info_file \"{cleanKeyInfo}\"",
                    "-hls_flags independent_segments",
                    "-hls_segment_type mpegts",
                    $"-hls_segment_filename \"{cleanTempDir}/seg%03d.ts\"",
                    $"\"{cleanTempDir}/index.m3u8\""
                );
            }

            // 🎬 Multi-Quality mode: 1080p (copy) + 720p (ultrafast) + 480p (ultrafast)
            return string.Join(" ",
                "-y",
                $"-i \"{inputPath}\"",
                "-force_key_frames \"expr:gte(t,n_forced*10)\"",
                "-map 0:v:0 -map 0:a:0?",
                "-map 0:v:0 -map 0:a:0?",
                "-map 0:v:0 -map 0:a:0?",
                "-c:v:0 copy -c:a:0 aac -b:a:0 128k",
                "-filter:v:1 scale=-2:720 -c:v:1 libx264 -preset ultrafast -crf 24 -c:a:1 aac -b:a:1 128k",
                "-filter:v:2 scale=-2:480 -c:v:2 libx264 -preset ultrafast -crf 26 -c:a:2 aac -b:a:2 96k",
                "-f hls",
                "-hls_time 10",
                "-hls_list_size 0",
                $"-hls_key_info_file \"{cleanKeyInfo}\"",
                "-hls_flags independent_segments",
                "-hls_segment_type mpegts",
                $"-hls_segment_filename \"{cleanTempDir}/seg_%v_%03d.ts\"",
                "-master_pl_name index.m3u8",
                "-var_stream_map \"v:0,a:0,name:1080p v:1,a:1,name:720p v:2,a:2,name:480p\"",
                $"\"{cleanTempDir}/%v.m3u8\""
            );
        }

        private async Task<HlsProcessingResult> RunFfmpegAsync(string args, IProgress<int>? progress, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var stderrBuffer = new StringBuilder();
            double totalSeconds = 0;
            var durationRegex = new Regex(@"Duration:\s*(\d+):(\d+):(\d+\.?\d*)", RegexOptions.Compiled);
            var timeRegex = new Regex(@"time=(\d+):(\d+):(\d+\.?\d*)", RegexOptions.Compiled);

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                stderrBuffer.AppendLine(e.Data);

                if (progress == null) return;

                if (totalSeconds <= 0)
                {
                    var dm = durationRegex.Match(e.Data);
                    if (dm.Success)
                    {
                        totalSeconds = int.Parse(dm.Groups[1].Value) * 3600
                                     + int.Parse(dm.Groups[2].Value) * 60
                                     + double.Parse(dm.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                if (totalSeconds > 0)
                {
                    var tm = timeRegex.Match(e.Data);
                    if (tm.Success)
                    {
                        var currentSeconds = int.Parse(tm.Groups[1].Value) * 3600
                                          + int.Parse(tm.Groups[2].Value) * 60
                                          + double.Parse(tm.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                        var pct = (int)(currentSeconds / totalSeconds * 100);
                        progress.Report(Math.Clamp(pct, 0, 99));
                    }
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new HlsProcessingResult { Success = false, Error = "Processing cancelled" };
            }

            if (process.ExitCode != 0)
            {
                return new HlsProcessingResult { Success = false, Error = $"FFmpeg failed with exit code {process.ExitCode}" };
            }

            return new HlsProcessingResult { Success = true };
        }

        /// <summary>
        /// 🔬 فاز ۲: تولید چانک‌های واریانت B (Forensic Watermarking)
        /// سگمنت‌های کد فرد (۱، ۳، ۵...) بازگشایی و با یک واترمارک هندسی دوگانه (Dual-Box) بسیار نامحسوس
        /// اما پایدار در برابر بازفشرده‌سازی بازتولید شده و با کلید و IV یکسان رمزنگاری می‌گردند.
        /// </summary>
        private async Task GenerateVariantBSegmentsAsync(
            string tempDir,
            byte[] keyBytes,
            string ivHex,
            CancellationToken cancellationToken)
        {
            var segmentFiles = Directory.GetFiles(tempDir, "*.ts")
                .Where(f => !f.EndsWith("_b.ts", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var segmentRegex = new Regex(@"seg(?:_([^_]+))?_?(\d+)\.ts$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var codeSegments = new List<(string SourcePath, string OutputPath, int SegIndex)>();

            foreach (var filePath in segmentFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var match = segmentRegex.Match(fileName);
                if (match.Success && int.TryParse(match.Groups[2].Value, out int segIndex))
                {
                    if (Application.Security.VideoForensics.IsCodeSegment(segIndex))
                    {
                        var dir = Path.GetDirectoryName(filePath)!;
                        var baseName = Path.GetFileNameWithoutExtension(filePath);
                        var outPath = Path.Combine(dir, $"{baseName}_b.ts");
                        codeSegments.Add((filePath, outPath, segIndex));
                    }
                }
            }

            if (codeSegments.Count == 0) return;

            _logger.LogInformation("Generating {Count} Variant B forensic watermarked segments...", codeSegments.Count);

            var ivBytes = Convert.FromHexString(ivHex);
            using var throttler = new SemaphoreSlim(4, 4);

            var tasks = codeSegments.Select(async item =>
            {
                await throttler.WaitAsync(cancellationToken);
                string tempRawIn = Path.Combine(tempDir, $"{Path.GetFileNameWithoutExtension(item.SourcePath)}_raw.ts");
                string tempRawOut = Path.Combine(tempDir, $"{Path.GetFileNameWithoutExtension(item.SourcePath)}_b_raw.ts");

                try
                {
                    // ۱. رمزگشایی سگمنت واریانت A به فرمت خام
                    using (var aes = Aes.Create())
                    {
                        aes.Key = keyBytes;
                        aes.IV = ivBytes;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using var decryptor = aes.CreateDecryptor();
                        using var inFs = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var outFs = new FileStream(tempRawIn, FileMode.Create, FileAccess.Write, FileShare.None);
                        using var cs = new CryptoStream(inFs, decryptor, CryptoStreamMode.Read);
                        await cs.CopyToAsync(outFs, cancellationToken);
                    }

                    // ۲. ترنس‌کد سریع چانک با فیلتر کنتراست‌تطبیقی دوگانه (Dual-Box: کادر خارجی تیره + هسته داخلی روشن)
                    var filter = "drawbox=x=w-64:y=24:w=32:h=32:color=black@0.25:t=fill,drawbox=x=w-60:y=28:w=24:h=24:color=white@0.30:t=fill";
                    var ffmpegArgs = $"-y -i \"{tempRawIn.Replace('\\', '/')}\" -vf \"{filter}\" -c:v libx264 -preset ultrafast -crf 20 -c:a copy -copyts -avoid_negative_ts 0 \"{tempRawOut.Replace('\\', '/')}\"";

                    var transcodeResult = await RunFfmpegAsync(ffmpegArgs, null, cancellationToken);

                    if (transcodeResult.Success && File.Exists(tempRawOut))
                    {
                        // ۳. رمزنگاری مجدد سگمنت واریانت B با همان کلید و IV برای پخش بدون درز
                        using (var aes = Aes.Create())
                        {
                            aes.Key = keyBytes;
                            aes.IV = ivBytes;
                            aes.Mode = CipherMode.CBC;
                            aes.Padding = PaddingMode.PKCS7;

                            using var encryptor = aes.CreateEncryptor();
                            using var inFs = new FileStream(tempRawOut, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var outFs = new FileStream(item.OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
                            using var cs = new CryptoStream(outFs, encryptor, CryptoStreamMode.Write);
                            await inFs.CopyToAsync(cs, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate Variant B for segment {Index}. Fallback to Variant A will occur automatically.", item.SegIndex);
                }
                finally
                {
                    try { if (File.Exists(tempRawIn)) File.Delete(tempRawIn); } catch { }
                    try { if (File.Exists(tempRawOut)) File.Delete(tempRawOut); } catch { }
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogInformation("✅ Variant B forensic segments successfully generated.");
        }
    }
}
