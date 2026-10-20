namespace Domain.Configuration
{
    public class VideoSecuritySettings
    {
        public HeartbeatSettings HeartbeatSettings { get; set; } = new();
        public BandwidthSettings BandwidthSettings { get; set; } = new();
        public PatternDetection PatternDetection { get; set; } = new();
        public ScoreWeights ScoreWeights { get; set; } = new();
        public ConnectionLimits ConnectionLimits { get; set; } = new();
    }

    public class HeartbeatSettings
    {
        public int IntervalSeconds { get; set; }
        public int MaxMissedHeartbeats { get; set; }
        public bool RequirePlayerState { get; set; }
    }

    public class BandwidthSettings
    {
        public int InitialBandwidthKBps { get; set; }
        public int MaxBandwidthKBps { get; set; }
        public int MinBandwidthKBps { get; set; }
        public bool ThrottleOnSuspicion { get; set; }
        public int SuspicionThrottleKBps { get; set; }
    }

    public class PatternDetection
    {
        public int MaxSeekPerMinute { get; set; }
        public int MaxPauseResumePerMinute { get; set; }
        public int MinWatchTimePercentage { get; set; }
        public int MaxSkipPercentage { get; set; }
        public bool DetectSequentialDownload { get; set; }
        public int SequentialChunkThreshold { get; set; }
    }

    public class ScoreWeights
    {
        public int NoHeartbeat { get; set; }
        public int NoPlayerState { get; set; }
        public int FastDownload { get; set; }
        public int SequentialPattern { get; set; }
        public int ExcessiveSeek { get; set; }
        public int LowWatchTime { get; set; }
        public int MultipleConnections { get; set; }
    }

    public class ConnectionLimits
    {
        public int MaxConnectionDurationMinutes { get; set; }
        public int MaxIdleTimeSeconds { get; set; }
        public int MaxBufferSizeKB { get; set; }
        public bool EnforcePlaybackVerification { get; set; }
    }
}
