using System.Collections.Concurrent;
using System.Security.Cryptography;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class IpBindingService : IIpBindingService
    {
        private readonly string _secretKey;
        private readonly ILogger<IpBindingService> _logger;
        private readonly ConcurrentDictionary<int, List<IpChangeRecord>> _ipChangeHistory;

        public IpBindingService(IConfiguration configuration, ILogger<IpBindingService> logger)
        {
            _secretKey = configuration["VideoSecurity:SecretKey"]
                ?? throw new ArgumentNullException(nameof(configuration), "VideoSecurity:SecretKey");
            _logger = logger;
            _ipChangeHistory = new ConcurrentDictionary<int, List<IpChangeRecord>>();
        }

        public string BindTokenToIp(string token, string ipAddress)
        {
            var ipHash = ComputeIpHash(ipAddress);
            var boundData = $"{token}|{ipHash}";

            using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(boundData));
            var signature = Convert.ToBase64String(hash);

            var boundToken = $"{boundData}|{signature}";
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(boundToken));
        }

        public bool ValidateIpBinding(string boundToken, string currentIpAddress)
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(boundToken));
                var parts = decoded.Split('|');

                if (parts.Length != 3)
                {
                    _logger.LogWarning("Invalid bound token format");
                    return false;
                }

                var originalToken = parts[0];
                var boundIpHash = parts[1];
                var signature = parts[2];

                var boundData = $"{originalToken}|{boundIpHash}";
                using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(_secretKey));
                var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(boundData));
                var expectedSignature = Convert.ToBase64String(hash);

                if (signature != expectedSignature)
                {
                    _logger.LogError("🚫 SECURITY: Invalid signature in bound token");
                    return false;
                }

                var currentIpHash = ComputeIpHash(currentIpAddress);
                if (boundIpHash != currentIpHash)
                {
                    _logger.LogError(
                        "🚫 SECURITY: IP mismatch - Token bound to different IP! Current: {CurrentIp}",
                        currentIpAddress
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating IP binding");
                return false;
            }
        }

        public void RecordIpChange(int userId, string oldIp, string newIp)
        {
            var history = _ipChangeHistory.GetOrAdd(userId, _ => new List<IpChangeRecord>());

            lock (history)
            {
                history.Add(new IpChangeRecord
                {
                    OldIp = oldIp,
                    NewIp = newIp,
                    ChangedAt = DateTime.UtcNow
                });

                if (history.Count > 20)
                {
                    history.RemoveRange(0, history.Count - 20);
                }
            }

            _logger.LogWarning(
                "⚠️ IP Change detected - UserId: {UserId}, Old: {OldIp}, New: {NewIp}",
                userId, oldIp, newIp
            );
        }

        public bool IsSuspiciousIpPattern(int userId, string ipAddress)
        {
            if (!_ipChangeHistory.TryGetValue(userId, out var history))
                return false;

            lock (history)
            {
                var recentChanges = history.Count(r =>
                    (DateTime.UtcNow - r.ChangedAt).TotalMinutes <= 10);

                if (recentChanges > 5)
                {
                    _logger.LogError(
                        "🚫 SUSPICIOUS: Too many IP changes - UserId: {UserId}, Changes: {Count} in 10 min",
                        userId, recentChanges
                    );
                    return true;
                }

                var recentIps = history
                    .Where(r => (DateTime.UtcNow - r.ChangedAt).TotalHours <= 1)
                    .Select(r => r.NewIp)
                    .Distinct()
                    .ToList();

                if (recentIps.Count > 3)
                {
                    _logger.LogError(
                        "🚫 SUSPICIOUS: Multiple different IPs - UserId: {UserId}, IPs: {Count} in 1 hour",
                        userId, recentIps.Count
                    );
                    return true;
                }
            }

            return false;
        }

        private string ComputeIpHash(string ipAddress)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(ipAddress + _secretKey));
            return Convert.ToBase64String(hashBytes);
        }
    }
}
