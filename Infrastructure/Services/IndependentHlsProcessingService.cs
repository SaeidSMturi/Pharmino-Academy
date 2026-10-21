using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

        public string GetSegmentPath(int courseId, string videoGuid, int segIndex, string? quality = null)
        {
            if (!string.IsNullOrEmpty(quality))
                return $"{GetHlsDirectory(courseId, videoGuid)}/seg_{quality}_{segIndex:D3}.ts";
            return $"{GetHlsDirectory(courseId, videoGuid)}/seg{segIndex:D3}.ts";
        }

        public async Task<bool> HlsExistsAsync(int courseId, string videoGuid) =>
            await _storageService.FileExistsAsync(GetHlsDirectory(courseId, videoGuid), "index.m3u8");

        public async Task<string> ReadPlaylistAsync(int courseId, string videoGuid) =>
            await _storageService.ReadFileTextAsync(GetHlsDirectory(courseId, videoGuid), "index.m3u8");

        public async Task<string> ReadVariantPlaylistAsync(int courseId, string videoGuid, string quality) =>
            await _storageService.ReadFileTextAsync(GetHlsDirectory(courseId, videoGuid), $"{quality}.m3u8");

        public async Task<byte[]> ReadKeyAsync(int courseId, string videoGuid) =>
            await _storageService.ReadFileBytesAsync(GetHlsDirectory(courseId, videoGuid), "enc.key");

        public async Task<byte[]> ReadSegmentAsync(int courseId, string videoGuid, int segIndex, string? quality = null)
        {
            string dir = GetHlsDirectory(courseId, videoGuid);
            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"seg_{quality}_{segIndex:D3}.ts";
                if (await _storageService.FileExistsAsync(dir, qualityFileName))
                    return await _storageService.ReadFileBytesAsync(dir, qualityFileName);
            }
            return await _storageService.ReadFileBytesAsync(dir, $"seg{segIndex:D3}.ts");
        }

        public async Task<(Stream fileStream, long contentLength)> GetSegmentStreamAsync(int courseId, string videoGuid, int segmentIndex, string? quality = null)
        {
            string directoryPath = GetHlsDirectory(courseId, videoGuid);
            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"seg_{quality}_{segmentIndex:D3}.ts";
                try
                {
                    return await _storageService.GetFileStreamAsync(directoryPath, qualityFileName);
                }
                catch (FileNotFoundException)
                {
                    // Fallback to legacy single-quality filename if variant not found
                }
            }
            string fallbackFileName = $"seg{segmentIndex:D3}.ts";
            return await _storageService.GetFileStreamAsync(directoryPath, fallbackFileName);
        }

        public async Task<int> GetSegmentCountAsync(int courseId, string videoGuid)
        {
            if (!await HlsExistsAsync(courseId, videoGuid)) return 0;
            var playlist = await ReadPlaylistAsync(courseId, videoGuid);
            return System.Text.RegularExpressions.Regex.Matches(playlist, @"#EXTINF:").Count;
        }

        public async Task<HlsProcessingResult> ProcessVideoAsync(
            int courseId, string inputFilePath, string videoGuid,
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

                var rawArguments = $"-y -i \"{inputFilePath}\" -map 0:v:0 -map 0:a:0? -map 0:v:0 -map 0:a:0? -map 0:v:0 -map 0:a:0? -c:v:0 copy -c:a:0 aac -b:a:0 128k -filter:v:1 scale=-2:720 -c:v:1 libx264 -preset ultrafast -crf 24 -c:a:1 aac -b:a:1 128k -filter:v:2 scale=-2:480 -c:v:2 libx264 -preset ultrafast -crf 26 -c:a:2 aac -b:a:2 96k -f hls -hls_time 5 -hls_list_size 0 -hls_key_info_file \"{cleanKeyInfo}\" -hls_flags independent_segments -hls_segment_type mpegts -hls_segment_filename \"{cleanTempDir}/seg_%v_%03d.ts\" -master_pl_name index.m3u8 -var_stream_map \"v:0,a:0,name:1080p v:1,a:1,name:720p v:2,a:2,name:480p\" \"{cleanTempDir}/%v.m3u8\"";

                _logger.LogInformation("Starting Multi-Quality HLS processing (Temp Dir) — CourseId: {CourseId}, VideoGuid: {Guid}", courseId, videoGuid);

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
    }
}
