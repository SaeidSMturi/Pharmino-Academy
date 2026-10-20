namespace Application.Interfaces
{
    public enum HlsJobStatus
    {
        Queued,
        Processing,
        Completed,
        Failed
    }

    public class HlsJobState
    {
        public int VideoId { get; set; }
        public HlsJobStatus Status { get; set; } = HlsJobStatus.Queued;
        public int Percent { get; set; } = 0;
        public string? Error { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public interface IHlsProgressService
    {
        void SetQueued(int videoId);
        void Report(int videoId, int percent);
        void SetCompleted(int videoId);
        void SetFailed(int videoId, string error);
        HlsJobState? Get(int videoId);
        void Remove(int videoId);
    }
}
