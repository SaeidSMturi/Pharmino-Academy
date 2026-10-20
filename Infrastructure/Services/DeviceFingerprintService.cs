using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services
{
    public class DeviceFingerprintService : IDeviceFingerprintService
    {
        public string GenerateFingerprint(HttpContext context)
        {
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return GenerateFingerprint(userAgent, ipAddress);
        }

        /// <summary>
        /// Combines the User-Agent string and IP address into a single SHA-256 hash. Using a
        /// one-way hash (rather than storing the raw values as the "fingerprint") means the bound cookie claim
        /// never needs to leak the client's raw IP/UA.
        /// </summary>
        public string GenerateFingerprint(string userAgent, string ipAddress)
        {
            var raw = $"{userAgent}|{ipAddress}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hashBytes);
        }
    }
}
