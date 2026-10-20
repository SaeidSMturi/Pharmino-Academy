using System.Collections.Concurrent;
using System.Security.Cryptography;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class OneTimeUrlService : IOneTimeUrlService
    {
        private readonly ConcurrentDictionary<string, OneTimeUrlData> _activeUrls;
        private readonly string _secretKey;
        private readonly ILogger<OneTimeUrlService> _logger;

        public OneTimeUrlService(IConfiguration configuration, ILogger<OneTimeUrlService> logger)
        {
            _activeUrls = new ConcurrentDictionary<string, OneTimeUrlData>();
            _secretKey = configuration["VideoSecurity:SecretKey"]
                ?? throw new ArgumentNullException(nameof(configuration), "VideoSecurity:SecretKey");
            _logger = logger;

            // پاکسازی هر 1 دقیقه
            StartCleanupTimer();
        }

        public string GenerateOneTimeUrl(int videoId, int userId, string sessionId, int ttlSeconds = 30)
        {
            var urlId = Guid.NewGuid().ToString("N");
            var expiration = DateTime.UtcNow.AddSeconds(ttlSeconds);

            var data = new OneTimeUrlData
            {
                UrlId = urlId,
                VideoId = videoId,
                UserId = userId,
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiration,
                IsConsumed = false
            };

            _activeUrls.TryAdd(urlId, data);

            // ساخت token امضا شده
            var payload = $"{urlId}:{videoId}:{userId}:{sessionId}:{new DateTimeOffset(expiration).ToUnixTimeSeconds()}";
            using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            var signature = Convert.ToBase64String(hash);

            var token = $"{payload}:{signature}";
            var encodedToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(token));

            _logger.LogInformation(
                "One-time URL generated - UrlId: {UrlId}, VideoId: {VideoId}, TTL: {TTL}s",
                urlId, videoId, ttlSeconds
            );

            return encodedToken;
        }

        public bool ValidateAndConsumeUrl(string urlToken, int videoId, int userId, string sessionId)
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(urlToken));
                var parts = decoded.Split(':');

                if (parts.Length != 6)
                {
                    _logger.LogWarning("Invalid one-time URL format");
                    return false;
                }

                var urlId = parts[0];
                var tokenVideoId = int.Parse(parts[1]);
                var tokenUserId = int.Parse(parts[2]);
                var tokenSessionId = parts[3];
                var tokenTimestamp = long.Parse(parts[4]);
                var tokenSignature = parts[5];

                // بررسی وجود URL
                if (!_activeUrls.TryGetValue(urlId, out var urlData))
                {
                    _logger.LogWarning(
                        "One-time URL not found or already consumed - UrlId: {UrlId}",
                        urlId
                    );
                    return false;
                }

                // بررسی انقضا
                if (DateTime.UtcNow > urlData.ExpiresAt)
                {
                    _activeUrls.TryRemove(urlId, out _);
                    _logger.LogWarning(
                        "One-time URL expired - UrlId: {UrlId}, Age: {Age}s",
                        urlId, (DateTime.UtcNow - urlData.CreatedAt).TotalSeconds
                    );
                    return false;
                }

                // بررسی مصرف قبلی
                if (urlData.IsConsumed)
                {
                    _logger.LogError(
                        "🚫 SECURITY: Attempt to reuse one-time URL - UrlId: {UrlId}, VideoId: {VideoId}",
                        urlId, videoId
                    );
                    return false;
                }

                // بررسی مطابقت داده‌ها
                if (tokenVideoId != videoId || tokenUserId != userId || tokenSessionId != sessionId)
                {
                    _logger.LogError(
                        "🚫 SECURITY: One-time URL data mismatch - UrlId: {UrlId}",
                        urlId
                    );
                    return false;
                }

                // بررسی امضا
                var payload = $"{urlId}:{tokenVideoId}:{tokenUserId}:{tokenSessionId}:{tokenTimestamp}";
                using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_secretKey));
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
                var expectedSignature = Convert.ToBase64String(hash);

                if (tokenSignature != expectedSignature)
                {
                    _logger.LogError(
                        "🚫 SECURITY BREACH: Invalid signature in one-time URL - UrlId: {UrlId}",
                        urlId
                    );
                    return false;
                }

                // مصرف URL (یکبار مصرف!)
                urlData.IsConsumed = true;
                urlData.ConsumedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "✅ One-time URL consumed - UrlId: {UrlId}, VideoId: {VideoId}, Age: {Age}s",
                    urlId, videoId, (DateTime.UtcNow - urlData.CreatedAt).TotalSeconds
                );

                // حذف بعد از 10 ثانیه (برای debugging)
                _ = Task.Delay(10000).ContinueWith(_ =>
                {
                    _activeUrls.TryRemove(urlId, out var _);
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating one-time URL");
                return false;
            }
        }

        public void CleanExpiredUrls()
        {
            var now = DateTime.UtcNow;
            var expiredUrls = _activeUrls.Values
                .Where(u => now > u.ExpiresAt || (u.IsConsumed && u.ConsumedAt.HasValue && (now - u.ConsumedAt.Value).TotalMinutes > 1))
                .Select(u => u.UrlId)
                .ToList();

            foreach (var urlId in expiredUrls)
            {
                _activeUrls.TryRemove(urlId, out _);
            }

            if (expiredUrls.Any())
            {
                _logger.LogDebug(
                    "Cleaned {Count} expired one-time URLs, Remaining: {Remaining}",
                    expiredUrls.Count, _activeUrls.Count
                );
            }
        }

        private void StartCleanupTimer()
        {
            var timer = new System.Threading.Timer(_ => CleanExpiredUrls(), null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
    }
}
