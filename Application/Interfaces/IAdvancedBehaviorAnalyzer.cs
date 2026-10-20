namespace Application.Interfaces
{
    /// <summary>
    /// 🧠 سیستم تحلیل رفتار پیشرفته با الگوریتم ML-like
    /// تشخیص دانلود‌کننده‌ها بر اساس الگوهای رفتاری
    /// </summary>
    public interface IAdvancedBehaviorAnalyzer
    {
        BehaviorAnalysisResult AnalyzeSession(string sessionId, BehaviorMetrics metrics);
        void RecordBehavior(string sessionId, BehaviorEvent behaviorEvent);
        double CalculateDownloadProbability(string sessionId);
        bool IsDefinitelyDownloader(string sessionId);
        BehaviorProfile GetBehaviorProfile(string sessionId);
    }

    #region Models

    /// <summary>
    /// پروفایل رفتاری یک session
    /// </summary>
    public class BehaviorProfile
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<BehaviorEvent> Events { get; set; } = new();
        public BehaviorAnalysisResult? LastAnalysis { get; set; }

        public void UpdateMetrics(BehaviorMetrics metrics)
        {
            // به‌روزرسانی metrics بر اساس events
        }
    }

    /// <summary>
    /// رویداد رفتاری
    /// </summary>
    public class BehaviorEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// معیارهای رفتاری
    /// </summary>
    public class BehaviorMetrics
    {
        // Connection Metrics
        public int MaxConcurrentConnections { get; set; }
        public int TotalConnections { get; set; }
        public TimeSpan AverageReconnectionInterval { get; set; }

        // Request Metrics
        public int TotalRequests { get; set; }
        public double RequestIntervalStdDev { get; set; }
        public TimeSpan MinRequestInterval { get; set; }
        public TimeSpan TotalPauseDuration { get; set; }
        public TimeSpan SessionDuration { get; set; }

        // Bandwidth Metrics
        public double AverageBandwidthKBps { get; set; }
        public double PeakBandwidthKBps { get; set; }
        public double BandwidthStdDev { get; set; }
        public long TotalBytesTransferred { get; set; }
        public TimeSpan VideoDuration { get; set; }

        // User Interaction Metrics
        public int TotalUserInteractions { get; set; }
        public bool HasMouseMovement { get; set; }
        public bool HasKeyboardActivity { get; set; }
        public int FocusChangeCount { get; set; }
        public TimeSpan HiddenDuration { get; set; }

        // Range Request Metrics
        public int TotalRangeRequests { get; set; }
        public double RangeSizeStdDev { get; set; }
        public int SequentialRangeRequests { get; set; }
        public int OverlappingRangeRequests { get; set; }
        public bool HasFullFileRequest { get; set; }

        // Heartbeat Metrics
        public int TotalHeartbeats { get; set; }
        public double HeartbeatIntervalStdDev { get; set; }
        public int UniquePlayerStates { get; set; }

        // Seek Metrics
        public int TotalSeeks { get; set; }
        public double SeekIntervalStdDev { get; set; }
        public int ForwardSeeks { get; set; }

        // Buffer Metrics
        public double AverageBufferHealth { get; set; }
        public int BufferingEvents { get; set; }

        // Playback Metrics
        public double AveragePlaybackSpeed { get; set; }
        public TimeSpan WatchedDuration { get; set; }

        // Analysis Metrics
        public double RandomnessScore { get; set; }
    }

    /// <summary>
    /// نتیجه تحلیل رفتار
    /// </summary>
    public class BehaviorAnalysisResult
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, double> Scores { get; set; } = new();
        public double DownloadProbability { get; set; }
        public List<BehaviorPattern> MatchedPatterns { get; set; } = new();
        public BehaviorVerdict Verdict { get; set; }
    }

    /// <summary>
    /// الگوی رفتاری
    /// </summary>
    public class BehaviorPattern
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string[] Conditions { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// verdict نهایی
    /// </summary>
    public enum BehaviorVerdict
    {
        LikelyLegitimate = 0,
        PossiblyLegitimate = 1,
        Suspicious = 2,
        ProbablyDownloader = 3,
        DefinitelyDownloader = 4
    }

    #endregion
}
