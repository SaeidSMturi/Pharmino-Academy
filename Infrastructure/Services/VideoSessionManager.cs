using System.Collections.Concurrent;
using Domain.Configuration;
using Application.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class VideoSessionManager : IVideoSessionManager
    {
        private readonly ConcurrentDictionary<string, VideoStreamSession> _activeSessions;
        private readonly int _maxConcurrentStreams;
        private readonly VideoSecuritySettings _securitySettings;
        private readonly ILogger<VideoSessionManager> _logger;
        private readonly IServiceProvider _serviceProvider;

        public VideoSessionManager(
            IConfiguration configuration,
            ILogger<VideoSessionManager> logger,
            IServiceProvider serviceProvider)
        {
            _activeSessions = new ConcurrentDictionary<string, VideoStreamSession>();
            _maxConcurrentStreams = configuration.GetValue<int>("VideoSecurity:MaxConcurrentStreams", 2);
            _logger = logger;
            _serviceProvider = serviceProvider;

            // بارگذاری تنظیمات امنیتی پیشرفته
            _securitySettings = new VideoSecuritySettings
            {
                HeartbeatSettings = configuration.GetSection("VideoSecurity:AdvancedProtection:HeartbeatSettings")
                    .Get<HeartbeatSettings>() ?? new HeartbeatSettings(),
                BandwidthSettings = configuration.GetSection("VideoSecurity:AdvancedProtection:BandwidthSettings")
                    .Get<BandwidthSettings>() ?? new BandwidthSettings(),
                PatternDetection = configuration.GetSection("VideoSecurity:AdvancedProtection:PatternDetection")
                    .Get<PatternDetection>() ?? new PatternDetection(),
                ScoreWeights = configuration.GetSection("VideoSecurity:AdvancedProtection:BehaviorScoring:ScoreWeights")
                    .Get<ScoreWeights>() ?? new ScoreWeights(),
                ConnectionLimits = configuration.GetSection("VideoSecurity:AdvancedProtection:ConnectionLimits")
                    .Get<ConnectionLimits>() ?? new ConnectionLimits()
            };

            // شروع تایمر پاکسازی و تحلیل
            StartAnalysisTimer();
        }

        public bool CanStartStream(int userId, int videoId)
        {
            CleanExpiredSessions();

            // 🔒 حذف session های قدیمی همین کاربر برای همین ویدیو
            // این کار باعث می‌شود که refresh صفحه مشکلی ایجاد نکند
            var oldSessions = _activeSessions.Values
                .Where(s => s.UserId == userId && s.VideoId == videoId)
                .ToList();

            foreach (var oldSession in oldSessions)
            {
                var sessionAge = DateTime.UtcNow - oldSession.LastActivity;

                // 🔒 اگر session بیش از 30 ثانیه inactive است، حذفش کن
                // این مدت کافی است تا refresh صفحه مشکلی ایجاد نکند
                if (sessionAge.TotalSeconds > 30)
                {
                    _activeSessions.TryRemove(oldSession.SessionId, out _);
                    _logger.LogInformation(
                        "🧹 Removed stale session for same user/video - SessionId: {SessionId}, Age: {Seconds}s",
                        oldSession.SessionId,
                        sessionAge.TotalSeconds
                    );
                }
                // 🔒 اگر session جدید است ولی heartbeat ندارد، هنوز فعال نیست
                else if (oldSession.LastHeartbeat == null && sessionAge.TotalSeconds > 10)
                {
                    _activeSessions.TryRemove(oldSession.SessionId, out _);
                    _logger.LogInformation(
                        "🧹 Removed session without heartbeat - SessionId: {SessionId}",
                        oldSession.SessionId
                    );
                }
            }

            // 🔒 شمارش فقط session های واقعاً فعال (که هارت‌بیت دارند یا خیلی جدید هستند)
            var activeCount = _activeSessions.Values
                .Where(s => s.UserId == userId)
                .Where(s =>
                    (DateTime.UtcNow - s.LastActivity).TotalSeconds < 60 && // فعالیت در 60 ثانیه اخیر
                    (s.LastHeartbeat != null || (DateTime.UtcNow - s.StartTime).TotalSeconds < 15) // heartbeat دارد یا تازه شروع شده
                )
                .Count();

            _logger.LogInformation(
                "CanStartStream check - UserId: {UserId}, VideoId: {VideoId}, ActiveStreams: {Active}, Max: {Max}",
                userId, videoId, activeCount, _maxConcurrentStreams
            );

            return activeCount < _maxConcurrentStreams;
        }

        public void StartStream(int userId, int videoId, string sessionId, string ipAddress, string userAgent)
        {
            // 🔒 ابتدا session های قبلی همین کاربر برای همین ویدیو را حذف کن
            // این کار refresh صفحه را بدون مشکل می‌کند
            var oldSessions = _activeSessions.Values
                .Where(s => s.UserId == userId && s.VideoId == videoId && s.SessionId != sessionId)
                .ToList();

            foreach (var oldSession in oldSessions)
            {
                _activeSessions.TryRemove(oldSession.SessionId, out _);
                _logger.LogInformation(
                    "🔄 Replaced old session with new one - OldSessionId: {OldSession}, NewSessionId: {NewSession}",
                    oldSession.SessionId, sessionId
                );
            }

            // اگر session قبلاً وجود داشت، فقط به‌روزرسانی کن
            if (_activeSessions.TryGetValue(sessionId, out var existingSession))
            {
                existingSession.LastActivity = DateTime.UtcNow;
                existingSession.ConnectionCount++;

                _logger.LogInformation(
                    "Session reused - SessionId: {SessionId}, UserId: {UserId}, Connections: {Count}",
                    sessionId, userId, existingSession.ConnectionCount
                );
                return;
            }

            var session = new VideoStreamSession
            {
                SessionId = sessionId,
                UserId = userId,
                VideoId = videoId,
                StartTime = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ConnectionCount = 1
            };

            _activeSessions.TryAdd(sessionId, session);

            _logger.LogInformation(
                "New stream started - SessionId: {SessionId}, UserId: {UserId}, VideoId: {VideoId}, IP: {IP}, Total Active: {Count}",
                sessionId, userId, videoId, ipAddress, _activeSessions.Count
            );
        }

        public void EndStream(string sessionId)
        {
            if (_activeSessions.TryRemove(sessionId, out var session))
            {
                var duration = (DateTime.UtcNow - session.StartTime).TotalMinutes;

                _logger.LogInformation(
                    "Stream ended - SessionId: {SessionId}, Duration: {Duration}min, " +
                    "TotalBytes: {Bytes}, SuspicionScore: {Score}",
                    sessionId, duration, session.TotalBytesTransferred, session.SuspicionScore
                );
            }
        }

        public int GetActiveStreamsCount(int userId)
        {
            return _activeSessions.Values.Count(s => s.UserId == userId);
        }

        public VideoStreamSession? GetSession(string sessionId)
        {
            _activeSessions.TryGetValue(sessionId, out var session);
            return session;
        }

        public void UpdateHeartbeat(string sessionId, string playerState, double position, double duration, double bufferHealth)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.LastHeartbeat = DateTime.UtcNow;
                session.LastActivity = DateTime.UtcNow;
                session.LastPlayerState = playerState;
                session.LastReportedPosition = position;
                session.LastReportedDuration = duration;
                session.IsPlayerActive = true;

                session.Heartbeats.Add(new HeartbeatEvent
                {
                    Time = DateTime.UtcNow,
                    PlayerState = playerState,
                    CurrentPosition = position,
                    Duration = duration,
                    BufferHealth = bufferHealth
                });

                // محدود کردن تعداد heartbeat های ذخیره شده
                if (session.Heartbeats.Count > 100)
                {
                    session.Heartbeats = session.Heartbeats.Skip(50).ToList();
                }

                _logger.LogDebug(
                    "Heartbeat received - SessionId: {SessionId}, State: {State}, Position: {Position}",
                    sessionId, playerState, position
                );
            }
        }

        public void RecordChunkRequest(string sessionId, long startByte, long endByte, long bytesTransferred, TimeSpan duration)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.LastActivity = DateTime.UtcNow;
                session.ChunkRequests.Add(new ChunkRequest
                {
                    RequestTime = DateTime.UtcNow,
                    StartByte = startByte,
                    EndByte = endByte,
                    BytesTransferred = bytesTransferred,
                    Duration = duration
                });

                // 🔒 Update bandwidth with moving window tracking
                session.UpdateBandwidthMetrics(bytesTransferred, duration);

                // محدود کردن تعداد chunk های ذخیره شده
                if (session.ChunkRequests.Count > 50)
                {
                    session.ChunkRequests = session.ChunkRequests.Skip(25).ToList();
                }
            }
        }

        public void RecordSeek(string sessionId, double fromPosition, double toPosition)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.SeekCount++;
                session.SeekEvents.Add(new SeekEvent
                {
                    Time = DateTime.UtcNow,
                    FromPosition = fromPosition,
                    ToPosition = toPosition,
                    IsForward = toPosition > fromPosition
                });

                // محدود کردن تعداد seek های ذخیره شده
                if (session.SeekEvents.Count > 50)
                {
                    session.SeekEvents = session.SeekEvents.Skip(25).ToList();
                }
            }
        }

        public int CalculateSuspicionScore(string sessionId)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                return session.CalculateSuspicionScore(_securitySettings);
            }
            return 0;
        }

        public bool ShouldBlockStream(string sessionId)
        {
            var score = CalculateSuspicionScore(sessionId);
            var blockThreshold = 85;

            if (score >= blockThreshold)
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    if (!session.IsBlocked)
                    {
                        session.IsBlocked = true;

                        _logger.LogWarning(
                            "🚫 Stream BLOCKED - SessionId: {SessionId}, Score: {Score}, Reasons: {Reasons}",
                            sessionId, score, string.Join(", ", session.SuspicionReasons.Keys)
                        );

                        _ = LogSuspiciousActivityAsync(session, score, "Stream blocked due to high suspicion score");
                    }
                }
                return true;
            }

            return false;
        }

        public bool ShouldThrottleStream(string sessionId)
        {
            var score = CalculateSuspicionScore(sessionId);
            var throttleThreshold = 70;

            if (score >= throttleThreshold)
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    if (!session.IsThrottled)
                    {
                        session.IsThrottled = true;

                        _logger.LogWarning(
                            "⚠️ Stream THROTTLED - SessionId: {SessionId}, Score: {Score}",
                            sessionId, score
                        );
                    }
                }
                return true;
            }

            return false;
        }

        public int GetCurrentBandwidthLimit(string sessionId)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                if (session.IsBlocked)
                {
                    return 0; // قطع کامل
                }

                if (session.IsThrottled)
                {
                    return _securitySettings.BandwidthSettings.SuspicionThrottleKBps;
                }

                // 🔒 More aggressive throttling based on score
                var score = session.SuspicionScore;
                if (score > 60)
                {
                    return _securitySettings.BandwidthSettings.SuspicionThrottleKBps;
                }
                else if (score > 40)
                {
                    var reduction = (score - 40) / 20.0; // 0 to 1
                    var maxBw = _securitySettings.BandwidthSettings.MaxBandwidthKBps;
                    var minBw = _securitySettings.BandwidthSettings.SuspicionThrottleKBps;
                    return (int)(maxBw - (reduction * (maxBw - minBw)));
                }

                return _securitySettings.BandwidthSettings.MaxBandwidthKBps;
            }

            return _securitySettings.BandwidthSettings.InitialBandwidthKBps;
        }

        private void CleanExpiredSessions()
        {
            var now = DateTime.UtcNow;

            var expiredSessions = _activeSessions.Values
                .Where(s => (now - s.LastActivity).TotalMinutes > 30)
                .Select(s => s.SessionId)
                .ToList();

            var abandonedSessions = _activeSessions.Values
                .Where(s => s.LastHeartbeat == null && (now - s.StartTime).TotalSeconds > 900)
                .Select(s => s.SessionId)
                .ToList();

            var allToRemove = expiredSessions.Union(abandonedSessions).Distinct().ToList();

            foreach (var sessionId in allToRemove)
            {
                if (_activeSessions.TryRemove(sessionId, out var session))
                {
                    var reason = abandonedSessions.Contains(sessionId) ? "abandoned (no heartbeat or background tab)" : "expired (long pause)";
                    _logger.LogInformation(
                        "🧹 Session removed ({Reason}) - SessionId: {SessionId}, Age: {Minutes:F1}min, HadHeartbeat: {HadHeartbeat}",
                        reason, sessionId, (now - session.StartTime).TotalMinutes, session.LastHeartbeat != null
                    );
                }
            }

            if (allToRemove.Any())
            {
                _logger.LogInformation(
                    "🧹 Cleanup completed - Removed: {Count}, Remaining: {Remaining}",
                    allToRemove.Count, _activeSessions.Count
                );
            }
        }

        private void StartAnalysisTimer()
        {
            var timer = new System.Threading.Timer(_ =>
            {
                CleanExpiredSessions();

                foreach (var session in _activeSessions.Values)
                {
                    if (session.IsBlocked)
                        continue;

                    var sessionAge = (DateTime.UtcNow - session.StartTime).TotalSeconds;
                    if (session.LastHeartbeat == null && sessionAge > 30)
                    {
                        continue;
                    }

                    var score = session.CalculateSuspicionScore(_securitySettings);

                    if (score >= 80)
                    {
                        _logger.LogError(
                            "🔴 HIGH RISK DETECTED - SessionId: {SessionId}, UserId: {UserId}, " +
                            "Score: {Score}, Reasons: {Reasons}",
                            session.SessionId, session.UserId, score,
                            string.Join(", ", session.SuspicionReasons.Select(r => $"{r.Key}:{r.Value}"))
                        );
                    }
                    else if (score >= 60)
                    {
                        _logger.LogWarning(
                            "⚠️ SUSPICIOUS ACTIVITY - SessionId: {SessionId}, Score: {Score}",
                            session.SessionId, score
                        );
                    }
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private async Task LogSuspiciousActivityAsync(VideoStreamSession session, int score, string reason)
        {
            try
            {
                if (session.SuspicionReasons.ContainsKey("_ActivityLogged"))
                {
                    _logger.LogDebug("Skipping duplicate suspicious activity log for session {SessionId}", session.SessionId);
                    return;
                }

                session.SuspicionReasons["_ActivityLogged"] = 1;

                using var scope = _serviceProvider.CreateScope();
                var suspiciousActivityService = scope.ServiceProvider.GetRequiredService<ISuspiciousActivityService>();

                var activityType = DetermineActivityType(session);

                var additionalData = System.Text.Json.JsonSerializer.Serialize(new
                {
                    session.TotalBytesTransferred,
                    session.AverageBandwidthKBps,
                    session.SeekCount,
                    session.ConnectionCount,
                    SessionDuration = (DateTime.UtcNow - session.StartTime).TotalMinutes,
                    SuspicionReasons = session.SuspicionReasons.Where(r => r.Key != "_ActivityLogged").ToDictionary(r => r.Key, r => r.Value),
                    LastPlayerState = session.LastPlayerState,
                    IsBlocked = session.IsBlocked,
                    IsThrottled = session.IsThrottled
                });

                await suspiciousActivityService.LogSuspiciousActivityAsync(
                    userId: session.UserId,
                    activityType: activityType,
                    suspicionScore: score,
                    reason: $"{reason} - {string.Join(", ", session.SuspicionReasons.Keys.Where(k => k != "_ActivityLogged").Take(3))}",
                    ipAddress: session.IpAddress,
                    userAgent: session.UserAgent,
                    courseId: null,
                    videoId: session.VideoId,
                    videoFileName: null,
                    additionalData: additionalData
                );

                if (score >= 90)
                {
                    var (shouldLock, count, lockReason) = await suspiciousActivityService
                        .CheckAndLockAccountIfNeededAsync(session.UserId, threshold: 10);

                    if (shouldLock)
                    {
                        session.IsBlocked = true;
                        _logger.LogCritical(
                            "🔒 ACCOUNT LOCKED - UserId: {UserId}, Count: {Count}, Reason: {Reason}",
                            session.UserId, count, lockReason
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging suspicious activity for user {UserId}", session.UserId);
            }
        }

        private string DetermineActivityType(VideoStreamSession session)
        {
            var reasons = session.SuspicionReasons.Keys.ToList();

            if (reasons.Any(k => k.Contains("BACKGROUND_DOWNLOAD_TRAP")))
                return "BackgroundLeechingTool";

            if (reasons.Any(k => k.Contains("SPEED_LIMIT_BYPASS")))
                return "SpeedLimitBypass";

            if (reasons.Any(k => k.Contains("VOLUME_ABUSE_TRAP")))
                return "VolumeAbuse";

            if (reasons.Any(k => k.Contains("EXCESSIVE_PARALLEL_STREAMS")))
                return "MultipleConnections";

            if (reasons.Any(k => k.Contains("RANGE_REQUEST_SPAM")))
                return "RangeRequestSpam";

            if (reasons.Any(k => k.Contains("SEQUENTIAL_DOWNLOAD_PATTERN")))
                return "SequentialDownload";

            if (reasons.Any(k => k.Contains("TELEMETRY_LAG") || k.Contains("INITIAL_HEARTBEAT_MISSING")))
                return "HeartbeatAnomaly";

            if (reasons.Any(k => k.Contains("SUSPICIOUS_SEEK_FREQUENCY")))
                return "ExcessiveSeek";

            return "GeneralSuspiciousActivity";
        }
    }
}
