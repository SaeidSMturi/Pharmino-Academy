using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Web.Services.Signup
{
    public enum SendCodeStatus { Sent, TooSoon, TooMany }

    public sealed record SendCodeResult(SendCodeStatus Status, int RetryAfterSeconds = 0);

    public enum VerifyStatus { Verified, Wrong, Locked, Expired }

    public sealed record VerifyResult(VerifyStatus Status, int TriesLeft = 0, string? Token = null);

    /// <summary>
    /// Email verification codes for sign-up, kept in memory only until the account is created.
    ///
    ///  - 4-digit codes (the size the OTP component was designed for), made safe by limits:
    ///    10-minute expiry, 5 wrong tries per code, a new code replaces the old one.
    ///  - Only a salted hash of the code is stored.
    ///  - Sending is limited: one code per email every 60 s (the OTP component's resend countdown),
    ///    5 per email per hour, 15 per IP per hour.
    ///  - A correct code returns a short-lived, single-use token. Account creation requires it,
    ///    so the account can't be created for an email that wasn't verified.
    ///
    /// The cache is per server process: on a web farm this would need a shared cache.
    /// </summary>
    public sealed class SignupVerificationService
    {
        public const int CodeLength = 4;
        public const int ResendSeconds = 60;
        private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(20);
        private const int MaxTries = 5;
        private const int MaxSendsPerEmailPerHour = 5;
        private const int MaxSendsPerIpPerHour = 15;

        private readonly IMemoryCache _cache;
        private readonly ITimeLimitedDataProtector _protector;

        private sealed class PendingCode
        {
            public byte[] Salt = Array.Empty<byte>();
            public byte[] Hash = Array.Empty<byte>();
            public DateTime ExpiresUtc;
            public int Tries;
        }

        public SignupVerificationService(IMemoryCache cache, IDataProtectionProvider dataProtection)
        {
            _cache = cache;
            _protector = dataProtection.CreateProtector("Signup.EmailVerified.v1").ToTimeLimitedDataProtector();
        }

        public static string Normalize(string email) => email.Trim().ToLowerInvariant();

        /// <summary>Creates a new code for the email (replacing any earlier one) if the limits allow. The caller emails it.</summary>
        public SendCodeResult TryCreateCode(string email, string ip, out string code)
        {
            code = string.Empty;
            var key = Normalize(email);
            var now = DateTime.UtcNow;

            if (_cache.TryGetValue("signup:last:" + key, out DateTime last) && now - last < TimeSpan.FromSeconds(ResendSeconds))
                return new SendCodeResult(SendCodeStatus.TooSoon, (int)Math.Ceiling(ResendSeconds - (now - last).TotalSeconds));

            if (Count("signup:sends:" + key) >= MaxSendsPerEmailPerHour || Count("signup:ipsends:" + ip) >= MaxSendsPerIpPerHour)
                return new SendCodeResult(SendCodeStatus.TooMany, 3600);

            code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeLength)).ToString(new string('0', CodeLength));
            var salt = RandomNumberGenerator.GetBytes(16);
            Set("signup:code:" + key, new PendingCode { Salt = salt, Hash = HashCode(salt, code), ExpiresUtc = now + CodeLifetime }, CodeLifetime);
            Set("signup:last:" + key, now, TimeSpan.FromSeconds(ResendSeconds));
            Increment("signup:sends:" + key, TimeSpan.FromHours(1));
            Increment("signup:ipsends:" + ip, TimeSpan.FromHours(1));
            return new SendCodeResult(SendCodeStatus.Sent);
        }

        /// <summary>Checks a code. On success the code is used up and a verification token is returned.</summary>
        public VerifyResult Verify(string email, string code)
        {
            var key = Normalize(email);
            if (!_cache.TryGetValue("signup:code:" + key, out PendingCode? pending) || pending == null)
                return new VerifyResult(VerifyStatus.Expired);
            if (DateTime.UtcNow > pending.ExpiresUtc)
            {
                _cache.Remove("signup:code:" + key);
                return new VerifyResult(VerifyStatus.Expired);
            }

            var ok = code.Length == CodeLength && code.All(char.IsDigit)
                     && CryptographicOperations.FixedTimeEquals(HashCode(pending.Salt, code), pending.Hash);
            if (!ok)
            {
                pending.Tries++;
                if (pending.Tries >= MaxTries)
                {
                    _cache.Remove("signup:code:" + key);
                    return new VerifyResult(VerifyStatus.Locked);
                }
                return new VerifyResult(VerifyStatus.Wrong, MaxTries - pending.Tries);
            }

            _cache.Remove("signup:code:" + key);
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            Set("signup:token:" + nonce, key, TokenLifetime);
            return new VerifyResult(VerifyStatus.Verified, Token: _protector.Protect(key + "|" + nonce, TokenLifetime));
        }

        /// <summary>
        /// True if the token was issued for this email, hasn't expired and hasn't been used. Using it consumes it,
        /// so call this only when the account is about to be created.
        /// </summary>
        public bool TryConsumeToken(string token, string email)
        {
            try
            {
                var parts = _protector.Unprotect(token).Split('|');
                if (parts.Length != 2 || parts[0] != Normalize(email)) return false;
                var cacheKey = "signup:token:" + parts[1];
                if (!_cache.TryGetValue(cacheKey, out string? owner) || owner != parts[0]) return false;
                _cache.Remove(cacheKey);
                return true;
            }
            catch (CryptographicException)
            {
                return false; // tampered or expired
            }
        }

        /// <summary>Simple fixed-window counter for rate limits on the check endpoints.</summary>
        public bool Allow(string bucket, int max, TimeSpan window)
        {
            if (Count(bucket) >= max) return false;
            Increment(bucket, window);
            return true;
        }

        private static byte[] HashCode(byte[] salt, string code) =>
            SHA256.HashData(salt.Concat(Encoding.UTF8.GetBytes(code)).ToArray());

        private int Count(string key) => _cache.TryGetValue(key, out int[]? c) && c != null ? c[0] : 0;

        private void Increment(string key, TimeSpan window)
        {
            if (_cache.TryGetValue(key, out int[]? c) && c != null) Interlocked.Increment(ref c[0]);
            else Set(key, new[] { 1 }, window);
        }

        // The app's memory cache has a SizeLimit, so every entry must declare a size.
        private void Set<T>(string key, T value, TimeSpan ttl) =>
            _cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Size = 1 });
    }
}
