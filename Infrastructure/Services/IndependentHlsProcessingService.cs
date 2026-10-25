using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Infrastructure.Services
{
    public class IndependentHlsProcessingService : IHlsProcessingService
    {
        private readonly string _enginePath;
        private readonly ILogger<IndependentHlsProcessingService> _logger;
        private readonly IStorageService _storageService;
        private volatile bool _isEngineReady = false;
        private readonly SemaphoreSlim _engineInitLock = new SemaphoreSlim(1, 1);

        public IndependentHlsProcessingService(
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<IndependentHlsProcessingService> logger,
            IStorageService storageService)
        {
            _logger = logger;
            _storageService = storageService;

            _enginePath = Path.Combine(env.ContentRootPath, "AppData", "VideoEngine");
            Directory.CreateDirectory(_enginePath);
        }

        private async Task EnsureEngineReadyAsync()
        {
            if (_isEngineReady) return;

            await _engineInitLock.WaitAsync();
            try
            {
                if (_isEngineReady) return;

                _logger.LogInformation("Setting up video processing engine...");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    FFmpeg.SetExecutablesPath(_enginePath);
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _enginePath);
                    _logger.LogInformation("Windows OS detected. FFmpeg downloaded to {Path}", _enginePath);
                }
                else
                {
                    FFmpeg.SetExecutablesPath("/usr/bin");
                    _logger.LogInformation("Linux OS detected. FFmpeg.SetExecutablesPath set to /usr/bin");
                }

                _isEngineReady = true;
            }
            finally
            {
                _engineInitLock.Release();
            }
        }

        public string GetHlsDirectory(int courseId, string videoGuid) =>
            $"uploads/courses/{courseId}/hls/{videoGuid}";

        public string GetPlaylistPath(int courseId, string videoGuid) =>
            $"{GetHlsDirectory(courseId, videoGuid)}/index.m3u8";

        public string GetKeyPath(int courseId, string videoGuid) =>
            $"{GetHlsDirectory(courseId, videoGuid)}/enc.key";

        public string GetSegmentPath(int courseId, string videoGuid, int segIndex, string? quality = null, string? variant = null)
        {
            var suffix = string.Equals(variant, "b", StringComparison.OrdinalIgnoreCase) ? "_b" : "";
            if (!string.IsNullOrEmpty(quality))
                return $"{GetHlsDirectory(courseId, videoGuid)}/seg_{quality}_{segIndex:D3}{suffix}.ts";
            return $"{GetHlsDirectory(courseId, videoGuid)}/seg{segIndex:D3}{suffix}.ts";
        }

        public async Task<bool> HlsExistsAsync(int courseId, string videoGuid) =>
            await _storageService.FileExistsAsync(GetHlsDirectory(courseId, videoGuid), "index.m3u8");

        public async Task<string> ReadPlaylistAsync(int courseId, string videoGuid) =>
            await _storageService.ReadFileTextAsync(GetHlsDirectory(courseId, videoGuid), "index.m3u8");

        public async Task<string> ReadVariantPlaylistAsync(int courseId, string videoGuid, string quality) =>
            await _storageService.ReadFileTextAsync(GetHlsDirectory(courseId, videoGuid), $"{quality}.m3u8");

        public async Task<byte[]> ReadKeyAsync(int courseId, string videoGuid) =>
            await _storageService.ReadFileBytesAsync(GetHlsDirectory(courseId, videoGuid), "enc.key");

        public async Task<byte[]> ReadSegmentAsync(int courseId, string videoGuid, int segIndex, string? quality = null, string? variant = null)
        {
            string dir = GetHlsDirectory(courseId, videoGuid);
            var isVariantB = string.Equals(variant, "b", StringComparison.OrdinalIgnoreCase);

            if (isVariantB)
            {
                if (!string.IsNullOrEmpty(quality))
                {
                    string bQuality = $"seg_{quality}_{segIndex:D3}_b.ts";
                    if (await _storageService.FileExistsAsync(dir, bQuality))
                        return await _storageService.ReadFileBytesAsync(dir, bQuality);
                }
                string bFile = $"seg{segIndex:D3}_b.ts";
                if (await _storageService.FileExistsAsync(dir, bFile))
                    return await _storageService.ReadFileBytesAsync(dir, bFile);
                string bAltFile = $"seg_{segIndex:D3}_b.ts";
                if (await _storageService.FileExistsAsync(dir, bAltFile))
                    return await _storageService.ReadFileBytesAsync(dir, bAltFile);
            }

            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"seg_{quality}_{segIndex:D3}.ts";
                if (await _storageService.FileExistsAsync(dir, qualityFileName))
                    return await _storageService.ReadFileBytesAsync(dir, qualityFileName);
            }
            string fallbackFileName = $"seg{segIndex:D3}.ts";
            if (await _storageService.FileExistsAsync(dir, fallbackFileName))
                return await _storageService.ReadFileBytesAsync(dir, fallbackFileName);
            return await _storageService.ReadFileBytesAsync(dir, $"seg_{segIndex:D3}.ts");
        }

        public async Task<(Stream fileStream, long contentLength)> GetSegmentStreamAsync(int courseId, string videoGuid, int segmentIndex, string? quality = null, string? variant = null)
        {
            string directoryPath = GetHlsDirectory(courseId, videoGuid);
            var isVariantB = string.Equals(variant, "b", StringComparison.OrdinalIgnoreCase);

            if (isVariantB)
            {
                if (!string.IsNullOrEmpty(quality))
                {
                    string bQuality = $"seg_{quality}_{segmentIndex:D3}_b.ts";
                    try { return await _storageService.GetFileStreamAsync(directoryPath, bQuality); }
                    catch (FileNotFoundException) { }
                }
                string bFile = $"seg{segmentIndex:D3}_b.ts";
                try { return await _storageService.GetFileStreamAsync(directoryPath, bFile); }
                catch (FileNotFoundException) { }
                string bAltFile = $"seg_{segmentIndex:D3}_b.ts";
                try { return await _storageService.GetFileStreamAsync(directoryPath, bAltFile); }
                catch (FileNotFoundException) { }
            }

            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"seg_{quality}_{segmentIndex:D3}.ts";
                try
                {
                    return await _storageService.GetFileStreamAsync(directoryPath, qualityFileName);
                }
                catch (FileNotFoundException)
                {
                    // Fallback to single-quality filename if variant not found
                }
            }
            string fallbackFileName = $"seg{segmentIndex:D3}.ts";
            try
            {
                return await _storageService.GetFileStreamAsync(directoryPath, fallbackFileName);
            }
            catch (FileNotFoundException)
            {
                string altFileName = $"seg_{segmentIndex:D3}.ts";
                return await _storageService.GetFileStreamAsync(directoryPath, altFileName);
            }
        }

        public async Task<int> GetSegmentCountAsync(int courseId, string videoGuid)
        {
            if (!await HlsExistsAsync(courseId, videoGuid)) return 0;
            var playlist = await ReadPlaylistAsync(courseId, videoGuid);
            return System.Text.RegularExpressions.Regex.Matches(playlist, @"#EXTINF:").Count;
        }

        public async Task<HlsProcessingResult> ProcessVideoAsync(
            int courseId, string inputFilePath, string videoGuid,
            bool multiQuality = false,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureEngineReadyAsync();

            var tempDir = Path.Combine(Path.GetTempPath(), $"hls_{videoGuid}");
            Directory.CreateDirectory(tempDir);

            var finalVirtualDir = GetHlsDirectory(courseId, videoGuid);

            try
            {
                var keyBytes = RandomNumberGenerator.GetBytes(16);
                var tempKeyPath = Path.Combine(tempDir, "enc.key");
                await File.WriteAllBytesAsync(tempKeyPath, keyBytes, cancellationToken);

                var ivHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                var keyInfoPath = Path.Combine(tempDir, "enc.keyinfo");

                await File.WriteAllLinesAsync(keyInfoPath, new[]
                {
                    "KEYHOLDER_PLACEHOLDER", // URI Placeholder
                    tempKeyPath,
                    ivHex
                }, cancellationToken);

                var tempPlaylistPath = Path.Combine(tempDir, "index.m3u8");

                var conversion = FFmpeg.Conversions.New();

                var stderrLog = new System.Text.StringBuilder();
                conversion.OnDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        stderrLog.AppendLine(args.Data);
                    }
                };

                if (progress != null)
                {
                    conversion.OnProgress += (_, args) =>
                    {
                        progress.Report(Math.Clamp(args.Percent, 0, 99));
                    };
                }

                var cleanTempDir = tempDir.Replace("\\", "/");
                var cleanKeyInfo = keyInfoPath.Replace("\\", "/");

                string rawArguments;
                if (!multiQuality)
                {
                    // ⚡ Fast Smart Copy mode
                    rawArguments = $"-y -i \"{inputFilePath}\" -c:v copy -c:a aac -b:a 128k -f hls -hls_time 10 -hls_list_size 0 -hls_key_info_file \"{cleanKeyInfo}\" -hls_flags independent_segments -hls_segment_type mpegts -hls_segment_filename \"{cleanTempDir}/seg%03d.ts\" \"{cleanTempDir}/index.m3u8\"";
                }
                else
                {
                    // 🎬 Multi-Quality mode
                    rawArguments = $"-y -i \"{inputFilePath}\" -force_key_frames \"expr:gte(t,n_forced*10)\" -map 0:v:0 -map 0:a:0? -map 0:v:0 -map 0:a:0? -map 0:v:0 -map 0:a:0? -c:v:0 copy -c:a:0 aac -b:a:0 128k -filter:v:1 scale=-2:720 -c:v:1 libx264 -preset ultrafast -crf 24 -c:a:1 aac -b:a:1 128k -filter:v:2 scale=-2:480 -c:v:2 libx264 -preset ultrafast -crf 26 -c:a:2 aac -b:a:2 96k -f hls -hls_time 10 -hls_list_size 0 -hls_key_info_file \"{cleanKeyInfo}\" -hls_flags independent_segments -hls_segment_type mpegts -hls_segment_filename \"{cleanTempDir}/seg_%v_%03d.ts\" -master_pl_name index.m3u8 -var_stream_map \"v:0,a:0,name:1080p v:1,a:1,name:720p v:2,a:2,name:480p\" \"{cleanTempDir}/%v.m3u8\"";
                }

                _logger.LogInformation("Starting HLS processing (Mode: {Mode}, hls_time: 10s) — CourseId: {CourseId}, VideoGuid: {Guid}",
                    multiQuality ? "Multi-Quality" : "Fast Smart Copy", courseId, videoGuid);

                try
                {
                    await conversion.AddParameter(rawArguments).Start(cancellationToken);
                }
                catch (Exception ffEx)
                {
                    var ffOutput = stderrLog.ToString();
                    _logger.LogError(ffEx, "FFmpeg conversion failed — CourseId: {CourseId}, VideoGuid: {Guid}.\nOutput: {Output}", courseId, videoGuid, ffOutput);
                    throw;
                }

                if (!File.Exists(tempPlaylistPath))
                    return new HlsProcessingResult { Success = false, Error = "Playlist not generated" };

                // 🔬 فاز ۲: تولید چانک‌های واریانت B (Forensic Watermarking) برای سگمنت‌های کد
                await GenerateVariantBSegmentsAsync(tempDir, keyBytes, ivHex, cancellationToken);

                _logger.LogInformation("🚀 Processing finished. Uploading HLS segments to Storage...");

                var filesToUpload = Directory.GetFiles(tempDir)
                    .Where(f => !f.EndsWith("enc.keyinfo"))
                    .ToList();

                var uploadTasks = filesToUpload.Select(async physicalFile =>
                {
                    var fileName = Path.GetFileName(physicalFile);
                    await _storageService.UploadPhysicalFileAsync(physicalFile, finalVirtualDir, fileName);
                });

                await Task.WhenAll(uploadTasks);

                var segmentCount = filesToUpload.Count(f => f.EndsWith(".ts"));
                _logger.LogInformation("✅ HLS uploaded successfully to Storage — Segments: {Count}", segmentCount);

                return new HlsProcessingResult
                {
                    Success = true,
                    PlaylistVirtualDirectory = GetPlaylistPath(courseId, videoGuid),
                    SegmentCount = segmentCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Independent HLS processing pipeline failed.");
                return new HlsProcessingResult { Success = false, Error = ex.Message };
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        await Task.Delay(2000); // مهلت کوتاه برای آزادسازی هندل‌ها در ویندوز
                        Directory.Delete(tempDir, recursive: true);
                        _logger.LogInformation("🧹 Temporary directory cleaned up: {Dir}", tempDir);
                    }
                }
                catch (Exception cleanEx)
                {
                    _logger.LogWarning(cleanEx, "Could not completely clean up temp directory {Dir}", tempDir);
                }
            }
        }

        /// <summary>
        /// 🔬 فاز ۲: تولید چانک‌های واریانت B (Forensic Watermarking) در محیط ویندوز
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

            _logger.LogInformation("Generating {Count} Variant B forensic watermarked segments (Windows)...", codeSegments.Count);

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

                    await FFmpeg.Conversions.New().AddParameter(ffmpegArgs).Start(cancellationToken);

                    if (File.Exists(tempRawOut))
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
