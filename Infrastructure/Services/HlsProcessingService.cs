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

        public async Task<byte[]> ReadKeyAsync(int courseId, string videoGuid) =>
            await _storageService.ReadFileBytesAsync(GetHlsDirectory(courseId, videoGuid), KeyFileName);

        public async Task<byte[]> ReadSegmentAsync(int courseId, string videoGuid, int segIndex) =>
            await _storageService.ReadFileBytesAsync(GetHlsDirectory(courseId, videoGuid), $"{SegmentPrefix}{segIndex:D3}{SegmentExtension}");

        public async Task<(Stream fileStream, long contentLength)> GetSegmentStreamAsync(int courseId, string videoGuid, int segmentIndex)
        {
            string fileName = $"{SegmentPrefix}{segmentIndex:D3}{SegmentExtension}";
            string directoryPath = GetHlsDirectory(courseId, videoGuid);
            return await _storageService.GetFileStreamAsync(directoryPath, fileName);
        }

        public async Task<int> GetSegmentCountAsync(int courseId, string videoGuid)
        {
            if (!await HlsExistsAsync(courseId, videoGuid)) return 0;
            var playlist = await ReadPlaylistAsync(courseId, videoGuid);
            return Regex.Matches(playlist, @"#EXTINF:").Count;
        }

        public string GetKeyPath(int courseId, string videoGuid) =>
            $"{GetHlsDirectory(courseId, videoGuid)}/{KeyFileName}";

        public string GetSegmentPath(int courseId, string videoGuid, int segIndex) =>
            $"{GetHlsDirectory(courseId, videoGuid)}/{SegmentPrefix}{segIndex:D3}{SegmentExtension}";

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
                var segmentPattern = Path.Combine(tempDir, $"{SegmentPrefix}%03d{SegmentExtension}");

                var ffmpegArgs = BuildFfmpegArgs(inputFilePath, tempPlaylistPath, segmentPattern, keyInfoPath);
                _logger.LogInformation("🎬 Starting FFmpeg locally — CourseId: {CourseId}, VideoGuid: {Guid}", courseId, videoGuid);

                var runResult = await RunFfmpegAsync(ffmpegArgs, progress, cancellationToken);
                if (!runResult.Success) return runResult;

                if (!File.Exists(tempPlaylistPath))
                    return new HlsProcessingResult { Success = false, Error = "Playlist not generated" };

                _logger.LogInformation("🚀 FFmpeg finished. Uploading HLS segments to Storage...");

                var filesToUpload = Directory.GetFiles(tempDir)
                    .Where(f => !f.EndsWith(KeyInfoFileName))
                    .ToList();

                await _storageService.UploadMultiplePhysicalFilesAsync(filesToUpload, finalStorageDir);

                var segmentCount = filesToUpload.Count(f => f.EndsWith(SegmentExtension));

                _logger.LogInformation("✅ HLS uploaded successfully — Segments: {Count}", segmentCount);

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

        private static string BuildFfmpegArgs(string inputPath, string playlistPath, string segmentPattern, string keyInfoPath)
        {
            return string.Join(" ",
                "-y",
                $"-i \"{inputPath}\"",
                "-c:v libx264 -preset fast -crf 22",
                "-c:a aac -b:a 128k",
                "-vf scale=-2:720",
                "-profile:v baseline -level:v 3.1",
                "-movflags +faststart",
                "-force_key_frames \"expr:gte(t,n_forced*5)\"",
                "-hls_time 5",
                "-hls_list_size 0",
                "-hls_segment_type mpegts",
                "-hls_flags independent_segments",
                $"-hls_segment_filename \"{segmentPattern}\"",
                $"-hls_key_info_file \"{keyInfoPath}\"",
                "-f hls",
                $"\"{playlistPath}\""
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
