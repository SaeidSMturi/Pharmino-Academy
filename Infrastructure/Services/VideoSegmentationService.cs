using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class VideoSegmentationService : IVideoSegmentationService
    {
        private readonly string _secretKey;
        private readonly ILogger<VideoSegmentationService> _logger;

        public VideoSegmentationService(IConfiguration configuration, ILogger<VideoSegmentationService> logger)
        {
            _secretKey = configuration["VideoSecurity:SecretKey"]
                ?? throw new ArgumentNullException(nameof(configuration), "VideoSecurity:SecretKey");
            _logger = logger;
        }

        public string GenerateSegmentToken(int videoId, int userId, int segmentIndex, DateTime expiration)
        {
            var timestamp = new DateTimeOffset(expiration).ToUnixTimeSeconds();
            var data = $"{videoId}:{userId}:{segmentIndex}:{timestamp}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var signature = Convert.ToBase64String(hash);

            var token = $"{data}:{signature}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        }

        public bool ValidateSegmentToken(string token, int videoId, int userId, int segmentIndex)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split(':');

                if (parts.Length != 5)
                {
                    _logger.LogWarning("Invalid token format");
                    return false;
                }

                var tokenVideoId = int.Parse(parts[0]);
                var tokenUserId = int.Parse(parts[1]);
                var tokenSegmentIndex = int.Parse(parts[2]);
                var tokenTimestamp = long.Parse(parts[3]);
                var tokenSignature = parts[4];

                // بررسی انقضا
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(tokenTimestamp).UtcDateTime;
                if (DateTime.UtcNow > expirationTime)
                {
                    _logger.LogWarning(
                        "Token expired - VideoId: {VideoId}, SegmentIndex: {Segment}",
                        videoId, segmentIndex
                    );
                    return false;
                }

                // بررسی مطاببات داده‌ها
                if (tokenVideoId != videoId || tokenUserId != userId || tokenSegmentIndex != segmentIndex)
                {
                    _logger.LogWarning(
                        "Token mismatch - Expected: V{VId}/U{UId}/S{Seg}, Got: V{TVId}/U{TUId}/S{TSeg}",
                        videoId, userId, segmentIndex,
                        tokenVideoId, tokenUserId, tokenSegmentIndex
                    );
                    return false;
                }

                // بررسی امضا
                var data = $"{tokenVideoId}:{tokenUserId}:{tokenSegmentIndex}:{tokenTimestamp}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                var expectedSignature = Convert.ToBase64String(hash);

                if (tokenSignature != expectedSignature)
                {
                    _logger.LogError(
                        "🚫 SECURITY BREACH: Invalid signature - VideoId: {VideoId}, UserId: {UserId}",
                        videoId, userId
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating segment token");
                return false;
            }
        }

        public (bool isValid, int segmentIndex) ParseSegmentToken(string token)
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split(':');

                if (parts.Length != 5)
                    return (false, -1);

                var segmentIndex = int.Parse(parts[2]);
                return (true, segmentIndex);
            }
            catch
            {
                return (false, -1);
            }
        }

        public string GenerateMasterPlaylist(int videoId, int userId, string baseUrl)
        {
            var totalSegments = 60;
            var segmentDuration = 10; // seconds

            var playlist = new StringBuilder();
            playlist.AppendLine("#EXTM3U");
            playlist.AppendLine("#EXT-X-VERSION:3");
            playlist.AppendLine($"#EXT-X-TARGETDURATION:{segmentDuration}");
            playlist.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
            playlist.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

            for (int i = 0; i < totalSegments; i++)
            {
                var expiration = DateTime.UtcNow.AddSeconds(30);
                var token = GenerateSegmentToken(videoId, userId, i, expiration);

                playlist.AppendLine($"#EXTINF:{segmentDuration}.0,");
                playlist.AppendLine($"{baseUrl}/segment/{i}?token={Uri.EscapeDataString(token)}");
            }

            playlist.AppendLine("#EXT-X-ENDLIST");

            return playlist.ToString();
        }
    }
}
