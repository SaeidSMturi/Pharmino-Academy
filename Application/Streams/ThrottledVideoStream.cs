using System.Diagnostics;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Streams
{
    /// <summary>
    /// Stream with precise mathematical throttling, behavioral monitoring, and heartbeat enforcement.
    /// Optimized for high-concurrency and seamless HLS/MP4 playback.
    /// </summary>
    public class ThrottledVideoStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly IVideoSessionManager _sessionManager;
        private readonly string _sessionId;
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;
        private readonly string _requestId;

        private long _totalBytesRead;
        private long _virtualPosition;
        private int _currentBandwidthLimitKBps;
        private DateTime _lastThrottleCheck;
        private DateTime _lastHeartbeatCheck;
        private DateTime _lastScoreCheck;
        private DateTime _lastRealTimeCheck;
        private bool _disposed;
        private bool _blockedByRealTime;

        // 🔒 SECURITY & STABILITY: تنظیمات بهینه‌شده
        // افزایش زمان ارفاق به 20 ثانیه. در شبکه‌های کند، لود اولیه پلیر و ارسال اولین Heartbeat زمان می‌برد.
        private const int GRACE_PERIOD_SECONDS = 20;
        private const int HEARTBEAT_TIMEOUT_SECONDS = 15;
        // بررسی لحظه‌ای هر 1 ثانیه یک‌بار کافی است تا فشار روی CPU سرور کاهش یابد
        private const int REALTIME_CHECK_INTERVAL_MS = 1000;

        public ThrottledVideoStream(
            Stream innerStream,
            IVideoSessionManager sessionManager,
            string sessionId,
            ILogger logger)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requestId = Guid.NewGuid().ToString("N").Substring(0, 8);

            _stopwatch = Stopwatch.StartNew();
            var now = DateTime.UtcNow;

            _lastThrottleCheck = now;
            _lastHeartbeatCheck = now;
            _lastScoreCheck = now;
            _lastRealTimeCheck = now;
            _blockedByRealTime = false;

            _totalBytesRead = 0;
            _virtualPosition = 0;

            _currentBandwidthLimitKBps = sessionManager.GetCurrentBandwidthLimit(sessionId);

            var session = _sessionManager.GetSession(sessionId);
            session?.IncrementParallelRequests();

            _logger.LogDebug(
                "🎬 ThrottledStream started - SessionId: {SessionId}, RequestId: {RequestId}, Concurrency: {Concurrency}, Limit: {Limit}KB/s",
                _sessionId, _requestId, session?.CurrentParallelRequests ?? 0, _currentBandwidthLimitKBps
            );
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _innerStream.Length;

        public override long Position
        {
            get => _innerStream.CanSeek ? _innerStream.Position : _virtualPosition;
            set
            {
                if (_innerStream.CanSeek)
                    _innerStream.Position = value;
                else
                    throw new NotSupportedException("This stream does not support seeking.");

                _virtualPosition = value;
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // اگر کاربر درخواست لغو داده باشد (مثلاً بستن تب مرورگر)، فوراً خارج می‌شویم تا منابع سرور آزاد شود
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTime.UtcNow;
            var elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            var session = _sessionManager.GetSession(_sessionId);

            // 1. اگر قبلاً بلاک شده، فوراً terminate کن
            if (_blockedByRealTime) return 0;

            // 2. Real-Time Threat Detection بهینه شده
            if ((now - _lastRealTimeCheck).TotalMilliseconds > REALTIME_CHECK_INTERVAL_MS)
            {
                _lastRealTimeCheck = now;

                if (session != null)
                {
                    var (shouldBlock, realTimeScore, realTimeReason) = session.CheckRealTimeThreats();

                    if (shouldBlock)
                    {
                        _blockedByRealTime = true;
                        _logger.LogWarning(
                            "🚫 REAL-TIME BLOCK - SessionId: {SessionId}, Score: {Score}, Reason: {Reason}",
                            _sessionId, realTimeScore, realTimeReason
                        );
                        return 0;
                    }
                }
            }

            // 3. بررسی بلاک بودن کلی سشن
            if (_sessionManager.ShouldBlockStream(_sessionId)) return 0;

            // 4. بررسی Heartbeat با ارفاق زمانی منطقی
            if (elapsedSeconds > GRACE_PERIOD_SECONDS)
            {
                if (session?.LastHeartbeat == null || (now - session.LastHeartbeat.Value).TotalSeconds > HEARTBEAT_TIMEOUT_SECONDS)
                {
                    _logger.LogInformation("⏳ Heartbeat timeout for SessionId: {SessionId}. Closing stream.", _sessionId);
                    return 0;
                }

                if ((now - _lastHeartbeatCheck).TotalSeconds >= 3)
                {
                    _lastHeartbeatCheck = now;
                }
            }

            // 5. آپدیت امتیاز مشکوک بودن (هر 5 ثانیه)
            if ((now - _lastScoreCheck).TotalSeconds >= 5)
            {
                _lastScoreCheck = now;
                _sessionManager.CalculateSuspicionScore(_sessionId);
            }

            // 6. به‌روزرسانی محدودیت سرعت به صورت زنده
            if ((now - _lastThrottleCheck).TotalSeconds >= 1)
            {
                _currentBandwidthLimitKBps = _sessionManager.GetCurrentBandwidthLimit(_sessionId);
                _lastThrottleCheck = now;
            }

            // اگر سرعت 0 تنظیم شده بود، یعنی دانلود متوقف شده است
            if (_currentBandwidthLimitKBps <= 0) return 0;

            // -------------------------------------------------------------
            // 💡 کنترل سرعت (Throttling) بهینه شده برای جلوگیری از پرش پلیر
            // -------------------------------------------------------------

            var startTime = _stopwatch.Elapsed;
            var startPosition = Position;

            var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

            if (bytesRead > 0)
            {
                _virtualPosition += bytesRead;

                double expectedMs = (bytesRead / 1024.0) / (_currentBandwidthLimitKBps / 1000.0);
                double actualReadMs = (_stopwatch.Elapsed - startTime).TotalMilliseconds;
                int delayMs = (int)(expectedMs - actualReadMs);

                // 🌟 کلید نرمی پلیر: Task.Delay در دات‌نت برای زیر 15 میلی‌ثانیه دقیق نیست
                // و فقط باعث استاتر (Stutter) می‌شود. تاخیرهای بسیار ریز را نادیده می‌گیریم.
                if (delayMs > 15)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }

                var totalDuration = _stopwatch.Elapsed - startTime;
                _totalBytesRead += bytesRead;

                _sessionManager.RecordChunkRequest(
                    _sessionId,
                    startPosition,
                    Position,
                    bytesRead,
                    totalDuration
                );
            }

            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!_innerStream.CanSeek)
                throw new NotSupportedException("This stream does not support seeking.");

            var currentPosition = Position;
            var result = _innerStream.Seek(offset, origin);
            _sessionManager.RecordSeek(_sessionId, currentPosition, Position);
            return result;
        }

        public override void Flush() => _innerStream.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _stopwatch.Stop();

                try
                {
                    var session = _sessionManager.GetSession(_sessionId);
                    session?.DecrementParallelRequests();
                }
                catch { /* در زمان Dispose نباید اروری به بالا نشت کند */ }

                _innerStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
