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

        public async Task<byte[]> ReadSegmentAsync(int courseId, string videoGuid, int segIndex, string? quality = null)
        {
            string dir = GetHlsDirectory(courseId, videoGuid);
            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"{SegmentPrefix}_{quality}_{segIndex:D3}{SegmentExtension}";
                if (await _storageService.FileExistsAsync(dir, qualityFileName))
                    return await _storageService.ReadFileBytesAsync(dir, qualityFileName);
            }
            return await _storageService.ReadFileBytesAsync(dir, $"{SegmentPrefix}{segIndex:D3}{SegmentExtension}");
        }

        public async Task<(Stream fileStream, long contentLength)> GetSegmentStreamAsync(int courseId, string videoGuid, int segmentIndex, string? quality = null)
        {
            string directoryPath = GetHlsDirectory(courseId, videoGuid);
            if (!string.IsNullOrEmpty(quality))
            {
                string qualityFileName = $"{SegmentPrefix}_{quality}_{segmentIndex:D3}{SegmentExtension}";
                try
                {
                    return await _storageService.GetFileStreamAsync(directoryPath, qualityFileName);
                }
                catch (FileNotFoundException)
                {
                    // Fallback to legacy single-quality filename if variant not found
                }
            }
            string fallbackFileName = $"{SegmentPrefix}{segmentIndex:D3}{SegmentExtension}";
            return await _storageService.GetFileStreamAsync(directoryPath, fallbackFileName);
        }

        public async Task<int> GetSegmentCountAsync(int courseId, string videoGuid)
        {
            if (!await HlsExistsAsync(courseId, videoGuid)) return 0;
            var playlist = await ReadPlaylistAsync(courseId, videoGuid);
            return Regex.Matches(playlist, @"#EXTINF:").Count;
        }

        public string GetKeyPath(int courseId, string videoGuid) =>
            $"{GetHlsDirectory(courseId, videoGuid)}/{KeyFileName}";

        public string GetSegmentPath(int courseId, string videoGuid, int segIndex, string? quality = null)
        {
            if (!string.IsNullOrEmpty(quality))
                return $"{GetHlsDirectory(courseId, videoGuid)}/{SegmentPrefix}_{quality}_{segIndex:D3}{SegmentExtension}";
            return $"{GetHlsDirectory(courseId, videoGuid)}/{SegmentPrefix}{segIndex:D3}{SegmentExtension}";
        }

        public async Task<HlsProcessingResult> ProcessVideoAsync(
            int courseId, string inputFilePath, string videoGuid,
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

                var ffmpegArgs = BuildFfmpegArgs(inputFilePath, tempDir, keyInfoPath);
                _logger.LogInformation("🎬 Starting Multi-Quality HLS FFmpeg (Smart Copy + Ultrafast) — CourseId: {CourseId}, VideoGuid: {Guid}", courseId, videoGuid);

                var runResult = await RunFfmpegAsync(ffmpegArgs, progress, cancellationToken);
                if (!runResult.Success) return runResult;

                if (!File.Exists(tempPlaylistPath))
                    return new HlsProcessingResult { Success = false, Error = "Playlist not generated" };

                _logger.LogInformation("🚀 FFmpeg finished. Uploading Multi-Quality HLS segments to Storage...");

                var filesToUpload = Directory.GetFiles(tempDir)
                    .Where(f => !f.EndsWith(KeyInfoFileName))
                    .ToList();

                await _storageService.UploadMultiplePhysicalFilesAsync(filesToUpload, finalStorageDir);

                var segmentCount = filesToUpload.Count(f => f.EndsWith(SegmentExtension));

                _logger.LogInformation("✅ Multi-Quality HLS uploaded successfully — Total Segments & Playlists: {Count}", filesToUpload.Count);

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

        private static string BuildFfmpegArgs(string inputPath, string tempDir, string keyInfoPath)
        {
            var cleanTempDir = tempDir.Replace("\\", "/");
            var cleanKeyInfo = keyInfoPath.Replace("\\", "/");

            return string.Join(" ",
                "-y",
                $"-i \"{inputPath}\"",
                "-map 0:v:0 -map 0:a:0?",
                "-map 0:v:0 -map 0:a:0?",
                "-map 0:v:0 -map 0:a:0?",
                "-c:v:0 copy -c:a:0 aac -b:a:0 128k",
                "-filter:v:1 scale=-2:720 -c:v:1 libx264 -preset ultrafast -crf 24 -c:a:1 aac -b:a:1 128k",
                "-filter:v:2 scale=-2:480 -c:v:2 libx264 -preset ultrafast -crf 26 -c:a:2 aac -b:a:2 96k",
                "-f hls",
                "-hls_time 5",
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
    }
}
