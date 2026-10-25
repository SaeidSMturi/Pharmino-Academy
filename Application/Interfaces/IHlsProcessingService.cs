namespace Application.Interfaces
{
    public class HlsProcessingResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? PlaylistVirtualDirectory { get; set; }
        public int SegmentCount { get; set; }
    }

    public interface IHlsProcessingService
    {
        Task<HlsProcessingResult> ProcessVideoAsync(
            int courseId, string inputFilePath, string videoGuid,
            bool multiQuality = false,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default);

        string GetHlsDirectory(int courseId, string videoGuid);
        Task<bool> HlsExistsAsync(int courseId, string videoGuid);
        Task<string> ReadPlaylistAsync(int courseId, string videoGuid);
        Task<string> ReadVariantPlaylistAsync(int courseId, string videoGuid, string quality);
        Task<byte[]> ReadKeyAsync(int courseId, string videoGuid);
        Task<byte[]> ReadSegmentAsync(int courseId, string videoGuid, int segIndex, string? quality = null, string? variant = null);

        // 🎬 GetSegmentStreamAsync
        Task<(Stream fileStream, long contentLength)> GetSegmentStreamAsync(int courseId, string videoGuid, int segmentIndex, string? quality = null, string? variant = null);

        Task<int> GetSegmentCountAsync(int courseId, string videoGuid);

        string GetKeyPath(int courseId, string videoGuid);
        string GetSegmentPath(int courseId, string videoGuid, int segIndex, string? quality = null, string? variant = null);
    }
}
