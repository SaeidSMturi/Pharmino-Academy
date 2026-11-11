using System.Security.Cryptography;
using System.Text;

namespace Web.Services
{
    /// <summary>
    /// The forensic code burned into the video watermark (e.g. PX-A042F3A1): user id + HMAC of user/video,
    /// keyed with <c>VideoSecurity:WatermarkSecret</c> so nobody can forge or predict another viewer's code.
    /// Used by both the student player and the admin preview, so the two always show the same code.
    /// </summary>
    public static class VideoForensicCode
    {
        public const string ConfigKey = "VideoSecurity:WatermarkSecret";

        public static string Create(IConfiguration configuration, int userId, int videoId)
        {
            var secret = configuration[ConfigKey];
            if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
                throw new InvalidOperationException($"'{ConfigKey}' must be set in appsettings.json (at least 32 random characters).");

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{userId}|{videoId}"));
            return $"PX-A{userId.ToString().PadLeft(3, '0')}{Convert.ToHexString(hash, 0, 2)}".ToUpperInvariant();
        }
    }
}
