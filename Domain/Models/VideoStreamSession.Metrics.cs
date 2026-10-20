namespace Domain.Models
{
    public partial class VideoStreamSession
    {
        public void UpdateBandwidthMetrics(long bytes, TimeSpan elapsed)
        {
            TotalBytesTransferred += bytes;

            double seconds = elapsed.TotalSeconds;
            if (seconds < 0.05) seconds = 0.05;

            var currentKBps = (bytes / 1024.0) / seconds;

            if (AverageBandwidthKBps == 0) AverageBandwidthKBps = currentKBps;
            else AverageBandwidthKBps = (AverageBandwidthKBps * 0.7) + (currentKBps * 0.3);

            var now = DateTime.UtcNow;
            if ((now - LastMetricsReset).TotalSeconds >= 30)
            {
                BytesServedLast30Seconds = 0;
                RequestsLast30Seconds = 0;
                LastMetricsReset = now;
            }

            BytesServedLast30Seconds += bytes;
            RequestsLast30Seconds++;
        }

        public void RecordChunkRequest(string sessionId, long startByte, long endByte, long bytesTransferred, TimeSpan duration)
        {
            var now = DateTime.UtcNow;

            // 🚨 محاسبه پیوستگی دانلود (Vacuum Trap Engine)
            if (LastChunkRequestTime != DateTime.MinValue)
            {
                var gap = (now - LastChunkRequestTime).TotalSeconds;
                // تغییر مهم: کاهش gap به 0.3 ثانیه. درخواست‌های HLS بسیار سریع و متوالی هستند
                // و مرورگر آنها را به سرعت پشت سر هم ارسال می‌کند.
                if (gap < 0.3)
                {
                    ContinuousDownloadBytes += bytesTransferred;
                }
                else
                {
                    ContinuousDownloadBytes = bytesTransferred; // ریست شدن پیوستگی با یک مکث طبیعی
                }
            }
            else
            {
                ContinuousDownloadBytes = bytesTransferred;
            }

            LastChunkRequestTime = now;

            ChunkRequests.Add(new ChunkRequest
            {
                RequestTime = now,
                StartByte = startByte,
                EndByte = endByte,
                BytesTransferred = bytesTransferred,
                Duration = duration
            });

            UpdateBandwidthMetrics(bytesTransferred, duration);

            if (ChunkRequests.Count > 50) ChunkRequests = ChunkRequests.Skip(25).ToList();
        }

        public void RecordRangeRequest(long startByte, long endByte)
        {
            var size = endByte - startByte + 1;
            var rangeReq = new RangeRequest { RequestTime = DateTime.UtcNow, StartByte = startByte, EndByte = endByte, Size = size };

            RangeRequests.Add(rangeReq);
            TotalRangeSize += size;

            if (size > LargestRangeSize) LargestRangeSize = size;
            if (RangeRequests.Count > 100) RangeRequests = RangeRequests.Skip(50).ToList();

            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            RangeRequestsLastMinute = RangeRequests.Count(r => r.RequestTime > oneMinuteAgo);

            if (RangeRequests.Count >= 2)
            {
                var lastTwo = RangeRequests.TakeLast(2).ToList();
                if (lastTwo[1].StartByte > lastTwo[0].EndByte &&
                    lastTwo[1].StartByte - lastTwo[0].EndByte < 1024 * 1024)
                {
                    SequentialRangeCount++;
                }
                else
                {
                    SequentialRangeCount = Math.Max(0, SequentialRangeCount - 1);
                }
            }
        }

        // 🔒 Thread-Safe Counters
        public void IncrementParallelRequests()
        {
            Interlocked.Increment(ref _currentParallelRequests);
            if (_currentParallelRequests > MaxParallelRequests)
                MaxParallelRequests = _currentParallelRequests;
        }

        public void DecrementParallelRequests()
        {
            if (_currentParallelRequests > 0)
                Interlocked.Decrement(ref _currentParallelRequests);
        }
    }
}
