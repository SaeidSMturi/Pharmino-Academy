using Domain.Configuration;

namespace Domain.Models
{
    public partial class VideoStreamSession
    {
        public int CalculateSuspicionScore(VideoSecuritySettings settings)
        {
            var baseScore = 0;
            SuspicionReasons.Clear();

            var now = DateTime.UtcNow;
            var sessionDuration = (now - StartTime).TotalSeconds;

            // دریافت نتایج آنی و ثبت مستقیم دلیل دقیق برای ادمین
            var (shouldBlock, realTimeScore, realTimeReason) = CheckRealTimeThreats();
            if (shouldBlock || realTimeScore > 0)
            {
                baseScore += realTimeScore;
                if (!string.IsNullOrEmpty(realTimeReason))
                {
                    // 💡 رفع باگ بزرگ: به جای استفاده از کلید ثابت "RealTimeThreat"، خود دلیل دقیق ذخیره می‌شود
                    SuspicionReasons[realTimeReason] = realTimeScore;
                }
            }

            if (sessionDuration < 10) return baseScore;

            // هارت‌بیت ثبت نشده است اما اتصال باز است
            if (LastHeartbeat == null)
            {
                baseScore += settings.ScoreWeights.NoHeartbeat;
                SuspicionReasons["BEHAVIOR_ANOMALY: Session has no telemetry events recorded yet."] = settings.ScoreWeights.NoHeartbeat;
            }
            else
            {
                var timeSinceLastHeartbeat = (now - LastHeartbeat.Value).TotalSeconds;
                var maxAllowedDelay = settings.HeartbeatSettings.MaxMissedHeartbeats * settings.HeartbeatSettings.IntervalSeconds;

                if (timeSinceLastHeartbeat > maxAllowedDelay)
                {
                    baseScore += settings.ScoreWeights.NoHeartbeat;
                    SuspicionReasons[$"TELEMETRY_LAG: Delay between heartbeats ({timeSinceLastHeartbeat:F0}s) exceeded limit."] = settings.ScoreWeights.NoHeartbeat;
                }
            }

            // بررسی اتصالات چندگانه مشکوک قبل از حد بلاک مستقیم
            if (_currentParallelRequests > 3)
            {
                baseScore += settings.ScoreWeights.MultipleConnections;
                SuspicionReasons[$"CONCURRENCY_WARNING: Active parallel HTTP connections count is {_currentParallelRequests}."] = settings.ScoreWeights.MultipleConnections;
            }

            // پترن دانلود متوالی چانک‌ها به سبک دانلودرها (Sequential Pattern)
            if (settings.PatternDetection.DetectSequentialDownload)
            {
                if (SequentialRangeCount >= settings.PatternDetection.SequentialChunkThreshold)
                {
                    baseScore += settings.ScoreWeights.SequentialPattern;
                    SuspicionReasons[$"SEQUENTIAL_DOWNLOAD_PATTERN: Client is scraping sequential bytes ({SequentialRangeCount} continuous ranges). Standard video buffering uses adaptive chunk allocation."] = settings.ScoreWeights.SequentialPattern;
                }
            }

            // جلو و عقب کردن بیش از حد نرمال (به طور مثال برای اکسپلویت کردن بافرینگ)
            var sessionMinutes = Math.Max(1, sessionDuration / 60.0);
            var seekPerMinute = SeekCount / sessionMinutes;

            if (seekPerMinute > settings.PatternDetection.MaxSeekPerMinute)
            {
                baseScore += settings.ScoreWeights.ExcessiveSeek;
                SuspicionReasons[$"SUSPICIOUS_SEEK_FREQUENCY: User triggered {seekPerMinute:F1} seek actions per minute. (Total: {SeekCount} seeks)."] = settings.ScoreWeights.ExcessiveSeek;
            }

            // امتیازدهی ترکیبی در زمان طولانی شدن نشست‌های مشکوک
            if (baseScore > 50)
            {
                CumulativeSuspicionBonus = Math.Min(20, (int)(sessionMinutes * 2));
                SuspicionReasons[$"CUMULATIVE_ESCALATION: Risk score amplified due to persistent abnormal behaviors over {sessionMinutes:F1} minutes."] = CumulativeSuspicionBonus;
            }

            BaseSuspicionScore = baseScore;
            SuspicionScore = baseScore + CumulativeSuspicionBonus;

            return SuspicionScore;
        }
    }
}
