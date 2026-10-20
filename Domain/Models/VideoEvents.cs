namespace Domain.Models
{
    public class ChunkRequest
    {
        public DateTime RequestTime { get; set; }
        public long StartByte { get; set; }
        public long EndByte { get; set; }
        public long BytesTransferred { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class RangeRequest
    {
        public DateTime RequestTime { get; set; }
        public long StartByte { get; set; }
        public long EndByte { get; set; }
        public long Size { get; set; }
    }

    public class SeekEvent
    {
        public DateTime Time { get; set; }
        public double FromPosition { get; set; }
        public double ToPosition { get; set; }
        public bool IsForward { get; set; }
    }

    public class HeartbeatEvent
    {
        public DateTime Time { get; set; }
        public string PlayerState { get; set; } = string.Empty;
        public double CurrentPosition { get; set; }
        public double Duration { get; set; }
        public double BufferHealth { get; set; }
    }
}
