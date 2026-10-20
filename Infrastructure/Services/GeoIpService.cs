using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;

namespace Infrastructure.Services
{
    /// <summary>
    /// پیاده‌سازی Mock/Deterministic سرویس GeoIP برای محیط توسعه و تست منطق سفر غیرممکن
    /// </summary>
    public class MockGeoIpService : IGeoIpService
    {
        // نقطه مرجع ثابت برای ترافیک محلی/توسعه (Loopback و شبکه‌های خصوصی) تا فاصله صفر محاسبه شود
        // Fixed reference point for local/dev traffic (loopback & private networks) so distance = 0 while testing.
        private static readonly GeoLocation LocalDevelopmentLocation = new(35.6892, 51.3890, "Tehran", "Iran");

        public Task<GeoLocation> GetLocationAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "unknown" || IsPrivateOrLoopback(ipAddress))
            {
                return Task.FromResult(LocalDevelopmentLocation);
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));

            // مشتق‌سازی قطعی مختصات از Hash آدرس IP (شبیه‌سازی رفتار یک پایگاه داده GeoIP واقعی)
            // Deterministically derive pseudo-coordinates from the IP hash (mimics a real GeoIP database lookup).
            var latitude = Math.Clamp((BitConverter.ToInt32(hash, 0) % 18000) / 100.0, -90, 90);
            var longitude = Math.Clamp((BitConverter.ToInt32(hash, 4) % 36000) / 100.0, -180, 180);

            return Task.FromResult(new GeoLocation(latitude, longitude, "Unknown", "Unknown"));
        }

        private static bool IsPrivateOrLoopback(string ip)
        {
            if (!IPAddress.TryParse(ip, out var address))
            {
                return false;
            }

            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }
    }
}
