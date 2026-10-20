using Domain.Configuration;

namespace Domain.Models
{
    /// <summary>
    /// مدل فوق پیشرفته برای تحلیل رفتار، تشخیص IDM و جلوگیری قطعی از سرقت ویدیو
    /// </summary>
    public partial class VideoStreamSession
    {
        public string SessionId { get; set; }
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastActivity { get; set; }
        public DateTime? LastHeartbeat { get; set; }
        public double TotalVideoSizeMB { get; set; }
        public readonly object HeartbeatsLock = new object();

        // Behavior Metrics
        public long TotalBytesTransferred { get; set; }
        public double AverageBandwidthKBps { get; set; }
        public int SeekCount { get; set; }
        public int PauseResumeCount { get; set; }
        public double TotalWatchedSeconds { get; set; }
        public double TotalPausedSeconds { get; set; }

        // 🚨 Vacuum Trap Metrics
        public long ContinuousDownloadBytes { get; set; }
        public DateTime LastChunkRequestTime { get; set; }

        // Cumulative metrics
        public long BytesServedLast30Seconds { get; set; }
        public int RequestsLast30Seconds { get; set; }

        // Thread-Safe Concurrency Tracker
        private int _currentParallelRequests;
        public int CurrentParallelRequests => _currentParallelRequests;
        public int MaxParallelRequests { get; set; }

        public int SequentialRangeCount { get; set; }
        public DateTime LastMetricsReset { get; set; }

        // Range abuse detection
        public List<RangeRequest> RangeRequests { get; set; } = new();
        public int RangeRequestsLastMinute { get; set; }
        public long TotalRangeSize { get; set; }
        public long LargestRangeSize { get; set; }

        // Pattern Detection
        public List<ChunkRequest> ChunkRequests { get; set; } = new();
        public List<SeekEvent> SeekEvents { get; set; } = new();
        public List<HeartbeatEvent> Heartbeats { get; set; } = new();

        // Suspicion Scoring
        public int SuspicionScore { get; set; }
        public int BaseSuspicionScore { get; set; }
        public int CumulativeSuspicionBonus { get; set; }
        public Dictionary<string, int> SuspicionReasons { get; set; } = new();
        public bool IsBlocked { get; set; }
        public bool IsThrottled { get; set; }

        // Player State
        public string LastPlayerState { get; set; } = string.Empty;
        public double LastReportedPosition { get; set; }
        public double LastReportedDuration { get; set; }
        public bool IsPlayerActive { get; set; }

        // Connection Info
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public int ConnectionCount { get; set; }

        public VideoStreamSession()
        {
            SessionId = Guid.NewGuid().ToString();
            StartTime = DateTime.UtcNow;
            LastActivity = DateTime.UtcNow;
            LastMetricsReset = DateTime.UtcNow;
            LastChunkRequestTime = DateTime.MinValue;
            IsPlayerActive = false;
        }
    }
}
