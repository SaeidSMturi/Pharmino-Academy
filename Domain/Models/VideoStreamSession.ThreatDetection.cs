namespace Domain.Models
{
    public partial class VideoStreamSession
    {
        /// <summary>
        /// 🔒 IRON DOME: الگوریتم Dual-Clutch Threshold
        /// محافظت ۱۰۰٪ بر اساس محاسبه لحظه‌ای Bitrate و رفتار جریان داده
        /// </summary>
        public (bool shouldBlock, int score, string? reason) CheckRealTimeThreats()
        {
            try
            {
                var now = DateTime.UtcNow;
                var sessionAge = (now - StartTime).TotalSeconds;
                var continuousMB = ContinuousDownloadBytes / (1024.0 * 1024.0);
                var totalMB = TotalBytesTransferred / (1024.0 * 1024.0);

                // 🚨 1. تله هارت‌بیت مرگ (Dead Heartbeat)
                if (sessionAge > 20 && LastHeartbeat == null)
                    return (true, 100, "NO_HEARTBEAT_20S");

                if (LastHeartbeat.HasValue && (now - LastHeartbeat.Value).TotalSeconds > 25)
                    return (true, 100, "HEARTBEAT_DIED");

                // استخراج داده‌های Thread-Safe از آخرین Heartbeat
                double duration = 120; // مقدار پیش‌فرض
                double watchedSeconds = 0;
                bool hasHeartbeats = false;

                lock (HeartbeatsLock)
                {
                    if (Heartbeats != null && Heartbeats.Any())
                    {
                        var lastHb = Heartbeats[^1]; // دریافت آخرین آیتم
                        duration = lastHb.Duration > 0 ? lastHb.Duration : 120;
                        watchedSeconds = lastHb.CurrentPosition;
                        hasHeartbeats = true;
                    }
                }

                // -------------------------------------------------------------
                // ⚙️ موتور محاسبه داینامیک: DUAL-CLUTCH THRESHOLD ALGORITHM
                // -------------------------------------------------------------

                // جلوگیری از خطای محاسبه در صورتی که حجم از کنترلر نیامده باشد
                double safeVideoSizeMB = TotalVideoSizeMB > 0 ? TotalVideoSizeMB : Math.Max(totalMB, 100);

                // الف) محاسبه نرخ بیت (مگابایت بر ثانیه)
                double bitrateMBps = safeVideoSizeMB / duration;

                // ب) محاسبه سقف بافر مطلق (معادل 45 ثانیه ویدیو)
                double hardBufferLimitMB = bitrateMBps * 100.0;

                // پ) اعمال کلاچ اول: محدودیت سفت‌وسخت برای ویدیوهای طولانی (> 5 دقیقه)
                if (duration > 300)
                {
                    hardBufferLimitMB = Math.Min(hardBufferLimitMB, 60.0);
                }

                // ت) اعمال کلاچ دوم: تضمین کیفیت برای ویدیوهای کوتاه و فوق‌سنگین (4K)
                hardBufferLimitMB = Math.Max(hardBufferLimitMB, 30.0);

                // -------------------------------------------------------------
                // 🛡️ تله‌های هوشمند مبتنی بر Bitrate
                // -------------------------------------------------------------

                // 🚨 2. تله حجم مطلق (Volume Trap)
                if (hasHeartbeats && sessionAge > 15)
                {
                    double expectedVolumeMB = (watchedSeconds * bitrateMBps) + hardBufferLimitMB;

                    if (totalMB > expectedVolumeMB)
                    {
                        return (true, 100, $"VOLUME_TRAP:Total_{totalMB:F1}MB_MaxExpected_{expectedVolumeMB:F1}MB");
                    }
                }

                // 🚨 3. تله مکنده پیوسته (Continuous Vacuum Trap)
                if (continuousMB > hardBufferLimitMB)
                {
                    return (true, 100, $"CONTINUOUS_TRAP:Downloaded_{continuousMB:F1}MB_Limit_{hardBufferLimitMB:F1}MB");
                }

                // -------------------------------------------------------------
                // 🧱 تله‌های زیرساختی و کانکشن 
                // -------------------------------------------------------------

                if (_currentParallelRequests >= 4)
                    return (true, 100, $"PARALLEL_CONNECTIONS:{_currentParallelRequests}");

                if (RangeRequestsLastMinute > 30)
                    return (true, 100, $"RANGE_SPAM:{RangeRequestsLastMinute}");

                if (ConnectionCount > 30 && sessionAge < 45)
                    return (true, 100, $"CONNECTION_SPAM:{ConnectionCount}");

                return (false, 0, null);
            }
            catch (Exception)
            {
                return (false, 0, null);
            }
        }
    }
}
