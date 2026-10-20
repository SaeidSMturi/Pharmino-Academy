using System.Collections.Concurrent;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class AdvancedBehaviorAnalyzer : IAdvancedBehaviorAnalyzer
    {
        private readonly ILogger<AdvancedBehaviorAnalyzer> _logger;
        private readonly ConcurrentDictionary<string, BehaviorProfile> _profiles = new();

        // 🔬 الگوهای رفتاری شناخته‌شده دانلود‌کننده‌ها
        private static readonly BehaviorPattern[] DownloaderPatterns = new[]
        {
            // IDM Pattern
            new BehaviorPattern
            {
                Name = "IDM_MULTI_CONNECTION",
                Description = "Internet Download Manager multi-connection pattern",
                Weight = 0.9,
                Conditions = new[]
                {
                    "ConcurrentConnections > 8",
                    "SequentialChunks > 10",
                    "NoHeartbeat",
                    "NoSeek"
                }
            },
            // wget/curl Pattern
            new BehaviorPattern
            {
                Name = "CLI_DOWNLOADER",
                Description = "Command-line downloader pattern",
                Weight = 0.95,
                Conditions = new[]
                {
                    "SingleConnection",
                    "FullFileRequest",
                    "NoRangeRequests",
                    "NoHeartbeat"
                }
            },
            // aria2 Pattern
            new BehaviorPattern
            {
                Name = "ARIA2_PATTERN",
                Description = "aria2 multi-threaded downloader",
                Weight = 0.85,
                Conditions = new[]
                {
                    "ConcurrentConnections > 5",
                    "FixedChunkSize",
                    "ParallelRangeRequests"
                }
            },
            // Screen Recorder Pattern
            new BehaviorPattern
            {
                Name = "SCREEN_RECORDER",
                Description = "Screen recording software pattern",
                Weight = 0.6,
                Conditions = new[]
                {
                    "SlowPlayback",
                    "NoSeeking",
                    "ConstantBitrate",
                    "FullDurationWatch"
                }
            },
            // Bot/Scraper Pattern
            new BehaviorPattern
            {
                Name = "BOT_SCRAPER",
                Description = "Automated bot or scraper",
                Weight = 0.8,
                Conditions = new[]
                {
                    "PerfectTiming",
                    "NoRandomness",
                    "ConsistentIntervals",
                    "NoUserInteraction"
                }
            }
        };

        public AdvancedBehaviorAnalyzer(ILogger<AdvancedBehaviorAnalyzer> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// تحلیل جامع session
        /// </summary>
        public BehaviorAnalysisResult AnalyzeSession(string sessionId, BehaviorMetrics metrics)
        {
            var profile = GetOrCreateProfile(sessionId);
            profile.UpdateMetrics(metrics);

            var result = new BehaviorAnalysisResult
            {
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                Scores = new Dictionary<string, double>()
            };

            // 1. محاسبه امتیاز Connection Pattern
            result.Scores["ConnectionPattern"] = AnalyzeConnectionPattern(profile, metrics);

            // 2. محاسبه امتیاز Request Timing
            result.Scores["RequestTiming"] = AnalyzeRequestTiming(profile, metrics);

            // 3. محاسبه امتیاز Bandwidth Pattern
            result.Scores["BandwidthPattern"] = AnalyzeBandwidthPattern(profile, metrics);

            // 4. محاسبه امتیاز User Interaction
            result.Scores["UserInteraction"] = AnalyzeUserInteraction(profile, metrics);

            // 5. محاسبه امتیاز Range Request Pattern
            result.Scores["RangePattern"] = AnalyzeRangePattern(profile, metrics);

            // 6. محاسبه امتیاز Heartbeat Pattern
            result.Scores["HeartbeatPattern"] = AnalyzeHeartbeatPattern(profile, metrics);

            // 7. محاسبه امتیاز Seek Pattern
            result.Scores["SeekPattern"] = AnalyzeSeekPattern(profile, metrics);

            // 8. محاسبه امتیاز Buffer Pattern
            result.Scores["BufferPattern"] = AnalyzeBufferPattern(profile, metrics);

            // محاسبه Download Probability با الگوریتم وزن‌دار
            result.DownloadProbability = CalculateWeightedProbability(result.Scores);

            // تطبیق با الگوهای شناخته‌شده
            result.MatchedPatterns = MatchKnownPatterns(profile, metrics);

            // تعیین verdict نهایی
            result.Verdict = DetermineVerdict(result);

            // ذخیره نتیجه در profile
            profile.LastAnalysis = result;

            if (result.DownloadProbability > 0.7)
            {
                _logger.LogWarning(
                    "🚨 HIGH DOWNLOAD PROBABILITY - SessionId: {SessionId}, Probability: {Prob:P2}, " +
                    "MatchedPatterns: [{Patterns}]",
                    sessionId, result.DownloadProbability,
                    string.Join(", ", result.MatchedPatterns.Select(p => p.Name)));
            }

            return result;
        }

        /// <summary>
        /// ثبت رویداد رفتاری
        /// </summary>
        public void RecordBehavior(string sessionId, BehaviorEvent behaviorEvent)
        {
            var profile = GetOrCreateProfile(sessionId);
            profile.Events.Add(behaviorEvent);

            // محدود کردن تعداد events
            if (profile.Events.Count > 500)
            {
                profile.Events = profile.Events.Skip(250).ToList();
            }
        }

        /// <summary>
        /// محاسبه احتمال دانلود
        /// </summary>
        public double CalculateDownloadProbability(string sessionId)
        {
            if (!_profiles.TryGetValue(sessionId, out var profile) || profile.LastAnalysis == null)
                return 0;

            return profile.LastAnalysis.DownloadProbability;
        }

        /// <summary>
        /// آیا قطعاً دانلود‌کننده است؟
        /// </summary>
        public bool IsDefinitelyDownloader(string sessionId)
        {
            var probability = CalculateDownloadProbability(sessionId);
            return probability >= 0.85; // 85% به بالا = قطعی
        }

        /// <summary>
        /// دریافت پروفایل رفتاری
        /// </summary>
        public BehaviorProfile? GetBehaviorProfile(string sessionId)
        {
            _profiles.TryGetValue(sessionId, out var profile);
            return profile;
        }

        #region Private Analysis Methods

        private BehaviorProfile GetOrCreateProfile(string sessionId)
        {
            return _profiles.GetOrAdd(sessionId, _ => new BehaviorProfile
            {
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// تحلیل الگوی Connection
        /// </summary>
        private double AnalyzeConnectionPattern(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // IDM معمولاً 8-16 connection باز می‌کند
            if (metrics.MaxConcurrentConnections > 8)
            {
                score += 0.4 * Math.Min(1.0, (metrics.MaxConcurrentConnections - 8) / 8.0);
            }

            // تعداد کل connection های یک session نباید زیاد باشد
            if (metrics.TotalConnections > 50)
            {
                score += 0.3 * Math.Min(1.0, metrics.TotalConnections / 100.0);
            }

            // بررسی reconnection سریع
            if (metrics.AverageReconnectionInterval < TimeSpan.FromSeconds(1))
            {
                score += 0.3;
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// تحلیل زمان‌بندی request ها
        /// </summary>
        private double AnalyzeRequestTiming(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // بررسی request های با فاصله یکسان (نشانه bot)
            if (metrics.RequestIntervalStdDev < 0.1 && metrics.TotalRequests > 10)
            {
                score += 0.5; // فاصله‌های خیلی یکسان → مشکوک
            }

            // بررسی request های خیلی سریع پشت سر هم
            if (metrics.MinRequestInterval < TimeSpan.FromMilliseconds(50))
            {
                score += 0.3;
            }

            // بررسی عدم وجود pause بین request ها
            if (metrics.TotalPauseDuration.TotalSeconds == 0 && metrics.SessionDuration.TotalMinutes > 2)
            {
                score += 0.2;
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// تحلیل الگوی Bandwidth
        /// </summary>
        private double AnalyzeBandwidthPattern(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // سرعت ثابت برای مدت طولانی → نشانه دانلود
            if (metrics.BandwidthStdDev < 100 && metrics.AverageBandwidthKBps > 1000)
            {
                score += 0.4;
            }

            // سرعت خیلی بالا
            if (metrics.PeakBandwidthKBps > 10000) // > 10 MB/s
            {
                score += 0.3;
            }

            // دانلود بیش از حد نسبت به طول ویدیو
            var expectedDownload = metrics.VideoDuration.TotalSeconds * 500; // ~500KB/s برای 1080p
            if (metrics.TotalBytesTransferred > expectedDownload * 1.5)
            {
                score += 0.3;
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// تحلیل تعامل کاربر
        /// </summary>
        private double AnalyzeUserInteraction(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // بدون هیچ تعامل
            if (metrics.TotalUserInteractions == 0 && metrics.SessionDuration.TotalMinutes > 1)
            {
                score += 0.5;
            }

            // بدون mouse movement
            if (!metrics.HasMouseMovement && metrics.SessionDuration.TotalMinutes > 2)
            {
                score += 0.2;
            }

            // بدون keyboard activity
            if (!metrics.HasKeyboardActivity)
            {
                score += 0.1;
            }

            // بدون focus changes
            if (metrics.FocusChangeCount == 0)
            {
                score += 0.1;
            }

            // Visibility همیشه visible
            if (metrics.HiddenDuration.TotalSeconds == 0 && metrics.SessionDuration.TotalMinutes > 5)
            {
                score += 0.1;
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// تحلیل الگوی Range Request
        /// </summary>
        private double AnalyzeRangePattern(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // Range های با سایز یکسان (نشانه chunk download)
            if (metrics.RangeSizeStdDev < 1000 && metrics.TotalRangeRequests > 10)
            {
                score += 0.4;
            }

            // Range های متوالی (sequential download)
            var sequentialRatio = metrics.SequentialRangeRequests / (double)Math.Max(1, metrics.TotalRangeRequests);
            if (sequentialRatio > 0.8)
            {
                score += 0.4;
            }

            // Range های همپوشان (overlap) → معمولاً نشانه download manager
            if (metrics.OverlappingRangeRequests > 5)
            {
                score += 0.2;
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// تحلیل الگوی Heartbeat
        /// </summary>
        private double AnalyzeHeartbeatPattern(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // بدون heartbeat
            if (metrics.TotalHeartbeats == 0)
            {
                score += 0.6;
            }
            else
            {
                // Heartbeat با فاصله خیلی دقیق → مشکوک (bot)
                if (metrics.HeartbeatIntervalStdDev < 0.2 && metrics.TotalHeartbeats > 5)
                {
                    score += 0.2;
                }

                // فقط playing state → مشکوک
                if (metrics.UniquePlayerStates == 1)
                {
                    score += 0.2;
                }
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// تحلیل الگوی Seek
        /// </summary>
        private double AnalyzeSeekPattern(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // بدون هیچ seek برای یک ویدیو طولانی
            if (metrics.TotalSeeks == 0 && metrics.VideoDuration.TotalMinutes > 5)
            {
                score += 0.4;
            }

            // Seek های با فاصله یکسان → مشکوک
            if (metrics.SeekIntervalStdDev < 5 && metrics.TotalSeeks > 5)
            {
                score += 0.3;
            }

            // همه seek ها به جلو → مشکوک
            var forwardRatio = metrics.ForwardSeeks / (double)Math.Max(1, metrics.TotalSeeks);
            if (forwardRatio == 1.0 && metrics.TotalSeeks > 3)
            {
                score += 0.3;
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// تحلیل الگوی Buffer
        /// </summary>
        private double AnalyzeBufferPattern(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var score = 0.0;

            // Buffer همیشه پر → نشانه دانلود سریع‌تر از پخش
            if (metrics.AverageBufferHealth > 95 && metrics.SessionDuration.TotalMinutes > 2)
            {
                score += 0.3;
            }

            // Buffer events بسیار کم
            if (metrics.BufferingEvents == 0 && metrics.SessionDuration.TotalMinutes > 3)
            {
                score += 0.2;
            }

            return Math.Min(1.0, score);
        }

        /// <summary>
        /// محاسبه احتمال وزن‌دار
        /// </summary>
        private double CalculateWeightedProbability(Dictionary<string, double> scores)
        {
            var weights = new Dictionary<string, double>
            {
                ["ConnectionPattern"] = 0.20,
                ["RequestTiming"] = 0.15,
                ["BandwidthPattern"] = 0.15,
                ["UserInteraction"] = 0.15,
                ["RangePattern"] = 0.10,
                ["HeartbeatPattern"] = 0.15,
                ["SeekPattern"] = 0.05,
                ["BufferPattern"] = 0.05
            };

            var weightedSum = scores.Sum(s => s.Value * weights.GetValueOrDefault(s.Key, 0.1));
            return Math.Min(1.0, weightedSum);
        }

        /// <summary>
        /// تطبیق با الگوهای شناخته‌شده
        /// </summary>
        private List<BehaviorPattern> MatchKnownPatterns(BehaviorProfile profile, BehaviorMetrics metrics)
        {
            var matched = new List<BehaviorPattern>();

            foreach (var pattern in DownloaderPatterns)
            {
                var matchScore = EvaluatePattern(pattern, metrics);
                if (matchScore >= 0.6)
                {
                    matched.Add(pattern);
                }
            }

            return matched;
        }

        /// <summary>
        /// ارزیابی تطابق یک الگو
        /// </summary>
        private double EvaluatePattern(BehaviorPattern pattern, BehaviorMetrics metrics)
        {
            var matchedConditions = 0;
            var totalConditions = pattern.Conditions.Length;

            foreach (var condition in pattern.Conditions)
            {
                if (EvaluateCondition(condition, metrics))
                    matchedConditions++;
            }

            return (double)matchedConditions / totalConditions;
        }

        /// <summary>
        /// ارزیابی یک شرط
        /// </summary>
        private bool EvaluateCondition(string condition, BehaviorMetrics metrics)
        {
            return condition switch
            {
                "ConcurrentConnections > 8" => metrics.MaxConcurrentConnections > 8,
                "ConcurrentConnections > 5" => metrics.MaxConcurrentConnections > 5,
                "SequentialChunks > 10" => metrics.SequentialRangeRequests > 10,
                "NoHeartbeat" => metrics.TotalHeartbeats == 0,
                "NoSeek" => metrics.TotalSeeks == 0,
                "SingleConnection" => metrics.MaxConcurrentConnections == 1,
                "FullFileRequest" => metrics.HasFullFileRequest,
                "NoRangeRequests" => metrics.TotalRangeRequests <= 1,
                "FixedChunkSize" => metrics.RangeSizeStdDev < 1000,
                "ParallelRangeRequests" => metrics.MaxConcurrentConnections > 3,
                "SlowPlayback" => metrics.AveragePlaybackSpeed < 0.8,
                "NoSeeking" => metrics.TotalSeeks == 0,
                "ConstantBitrate" => metrics.BandwidthStdDev < 200,
                "FullDurationWatch" => metrics.WatchedDuration >= metrics.VideoDuration * 0.9,
                "PerfectTiming" => metrics.RequestIntervalStdDev < 0.1,
                "NoRandomness" => metrics.RandomnessScore < 0.2,
                "ConsistentIntervals" => metrics.RequestIntervalStdDev < 0.2,
                "NoUserInteraction" => metrics.TotalUserInteractions == 0,
                _ => false
            };
        }

        /// <summary>
        /// تعیین verdict نهایی
        /// </summary>
        private BehaviorVerdict DetermineVerdict(BehaviorAnalysisResult result)
        {
            if (result.DownloadProbability >= 0.9)
                return BehaviorVerdict.DefinitelyDownloader;

            if (result.DownloadProbability >= 0.75)
                return BehaviorVerdict.ProbablyDownloader;

            if (result.DownloadProbability >= 0.5)
                return BehaviorVerdict.Suspicious;

            if (result.DownloadProbability >= 0.25)
                return BehaviorVerdict.PossiblyLegitimate;

            return BehaviorVerdict.LikelyLegitimate;
        }

        #endregion
    }
}
