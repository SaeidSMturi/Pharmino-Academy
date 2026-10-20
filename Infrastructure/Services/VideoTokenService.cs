using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;

namespace Infrastructure.Services
{
    public class VideoTokenService : IVideoTokenService
    {
        private readonly string _secretKey;
        private readonly string _sessionKey;
        private readonly string _oneTimeKey;

        // 🔒 One-Time Token Storage
        private static readonly ConcurrentDictionary<string, OneTimeTokenInfo> _oneTimeTokens = new();

        // 🔒 Used Tokens
        private static readonly ConcurrentDictionary<string, DateTime> _usedTokens = new();

        // 🔒 Token Request Counter
        private static readonly ConcurrentDictionary<string, TokenRequestCounter> _tokenRequestCounters = new();

        public VideoTokenService(IConfiguration configuration)
        {
            _secretKey = configuration["VideoSecurity:SecretKey"] ?? "YourSuperSecretKeyForVideoProtection2024!@#$%";
            _sessionKey = configuration["VideoSecurity:SessionSecretKey"] ?? _secretKey + "_session";
            _oneTimeKey = configuration["VideoSecurity:OneTimeTokenKey"] ?? _secretKey + "_onetime_v2";

            // Cleanup old tokens every 5 minutes
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    CleanupExpiredTokens();
                }
            });
        }

        public string GenerateToken(int videoId, int userId, int validMinutes = 60)
        {
            var expirationTime = DateTimeOffset.UtcNow.AddMinutes(validMinutes).ToUnixTimeSeconds();
            var data = $"{videoId}:{userId}:{expirationTime}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var signature = Convert.ToBase64String(hash);

            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data}:{signature}"));
            return token;
        }

        public bool ValidateToken(string token, int videoId, int userId)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split(':');

                if (parts.Length != 4)
                    return false;

                var tokenVideoId = int.Parse(parts[0]);
                var tokenUserId = int.Parse(parts[1]);
                var expirationTime = long.Parse(parts[2]);
                var signature = parts[3];

                // بررسی انقضا
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expirationTime)
                    return false;

                // بررسی videoId و userId
                if (tokenVideoId != videoId || tokenUserId != userId)
                    return false;

                // بررسی امضا با constant-time comparison
                var data = $"{tokenVideoId}:{tokenUserId}:{expirationTime}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                var expectedSignature = Convert.ToBase64String(hash);

                return ConstantTimeEquals(signature, expectedSignature);
            }
            catch
            {
                return false;
            }
        }

        public string GenerateSessionId(int videoId, int userId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nonce = Guid.NewGuid().ToString("N");
            var data = $"{userId}:{videoId}:{timestamp}:{nonce}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sessionKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var signature = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "");

            var sessionId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data}:{signature}"))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");

            return sessionId;
        }

        public bool ValidateSessionId(string sessionId, int videoId, int userId)
        {
            try
            {
                // Decode session ID
                var paddedSessionId = sessionId.Replace("-", "+").Replace("_", "/");
                paddedSessionId = paddedSessionId.PadRight(paddedSessionId.Length + (4 - paddedSessionId.Length % 4) % 4, '=');

                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(paddedSessionId));
                var parts = decoded.Split(':');

                if (parts.Length != 5)
                    return false;

                var sessionUserId = int.Parse(parts[0]);
                var sessionVideoId = int.Parse(parts[1]);
                var timestamp = long.Parse(parts[2]);
                var nonce = parts[3];
                var signature = parts[4];

                // Verify userId and videoId match
                if (sessionUserId != userId || sessionVideoId != videoId)
                    return false;

                // Verify session is not too old (max 24 hours)
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now - timestamp > 86400)
                    return false;

                // Verify HMAC signature
                var data = $"{sessionUserId}:{sessionVideoId}:{timestamp}:{nonce}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sessionKey));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                var expectedSignature = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "");

                return ConstantTimeEquals(signature, expectedSignature);
            }
            catch
            {
                return false;
            }
        }

        private bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            var result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        #region One-Time Token System

        public string GenerateOneTimeStreamToken(int videoId, int userId, string sessionId)
        {
            var counterKey = $"{userId}:{videoId}";
            var counter = _tokenRequestCounters.GetOrAdd(counterKey, _ => new TokenRequestCounter());

            if (!counter.CanRequest())
            {
                throw new InvalidOperationException("Too many token requests. Please wait.");
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var nonce = Guid.NewGuid().ToString("N")[..16]; // 16 char nonce
            var data = $"{videoId}:{userId}:{sessionId}:{timestamp}:{nonce}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_oneTimeKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var signature = Convert.ToBase64String(hash)[..22]; // Shortened signature

            var tokenData = $"{timestamp}:{nonce}:{signature}";
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');

            var tokenInfo = new OneTimeTokenInfo
            {
                VideoId = videoId,
                UserId = userId,
                SessionId = sessionId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(30), // 30 seconds
                IsUsed = false
            };

            _oneTimeTokens[token] = tokenInfo;

            return token;
        }

        public (bool isValid, string error) ValidateAndConsumeOneTimeToken(string token, int videoId, int userId, string sessionId)
        {
            if (string.IsNullOrEmpty(token))
                return (false, "Token is required");

            if (_usedTokens.ContainsKey(token))
                return (false, "Token already used (replay attack detected)");

            if (!_oneTimeTokens.TryGetValue(token, out var tokenInfo))
                return (false, "Invalid or expired token");

            if (DateTime.UtcNow > tokenInfo.ExpiresAt)
            {
                _oneTimeTokens.TryRemove(token, out _);
                return (false, "Token expired");
            }

            if (tokenInfo.IsUsed)
            {
                return (false, "Token already consumed");
            }

            if (tokenInfo.VideoId != videoId || tokenInfo.UserId != userId)
                return (false, "Token mismatch");

            tokenInfo.IsUsed = true;
            tokenInfo.UsedAt = DateTime.UtcNow;

            _usedTokens[token] = DateTime.UtcNow;
            _oneTimeTokens.TryRemove(token, out _);

            return (true, null);
        }

        #endregion

        #region Request Signature

        public string GenerateRequestSignature(string sessionId, long timestamp, long rangeStart, long rangeEnd)
        {
            var data = $"{sessionId}:{timestamp}:{rangeStart}:{rangeEnd}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_oneTimeKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash)[..16].Replace("+", "-").Replace("/", "_");
        }

        public bool ValidateRequestSignature(string signature, string sessionId, long timestamp, long rangeStart, long rangeEnd)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestamp) > 60)
                return false;

            var expectedSignature = GenerateRequestSignature(sessionId, timestamp, rangeStart, rangeEnd);
            return ConstantTimeEquals(signature, expectedSignature);
        }

        #endregion

        #region Cleanup

        private void CleanupExpiredTokens()
        {
            var now = DateTime.UtcNow;

            var expiredTokens = _oneTimeTokens
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var token in expiredTokens)
            {
                _oneTimeTokens.TryRemove(token, out _);
            }

            var oldUsedTokens = _usedTokens
                .Where(kvp => (now - kvp.Value).TotalMinutes > 10)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var token in oldUsedTokens)
            {
                _usedTokens.TryRemove(token, out _);
            }

            var oldCounters = _tokenRequestCounters
                .Where(kvp => (now - kvp.Value.LastReset).TotalMinutes > 5)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldCounters)
            {
                _tokenRequestCounters.TryRemove(key, out _);
            }
        }

        #endregion
    }

    #region Helper Classes

    internal class OneTimeTokenInfo
    {
        public int VideoId { get; set; }
        public int UserId { get; set; }
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
    }

    internal class TokenRequestCounter
    {
        private int _count;
        private DateTime _windowStart;
        private readonly object _lock = new();

        public DateTime LastReset => _windowStart;

        public TokenRequestCounter()
        {
            _windowStart = DateTime.UtcNow;
            _count = 0;
        }

        public bool CanRequest(int maxPerMinute = 100)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;

                if ((now - _windowStart).TotalMinutes >= 1)
                {
                    _windowStart = now;
                    _count = 0;
                }

                if (_count >= maxPerMinute)
                    return false;

                _count++;
                return true;
            }
        }
    }

    #endregion
}
