using System.Net;
using System.Net.Sockets;
using Application.Interfaces;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// GeoIP lookups against a local MaxMind GeoLite2-City database (.mmdb). The lookup happens on this
    /// server, so visitors' IP addresses are never sent to a third party.
    ///
    /// The database is free but needs a MaxMind account to download, and should be refreshed monthly.
    /// Its path comes from the "GeoIp:DatabasePath" setting.
    /// </summary>
    public sealed class MaxMindGeoIpService : IGeoIpService, IDisposable
    {
        private readonly DatabaseReader _reader;
        private readonly ILogger<MaxMindGeoIpService> _logger;

        public MaxMindGeoIpService(string databasePath, ILogger<MaxMindGeoIpService> logger)
        {
            // The reader is thread-safe and memory-maps the file; one instance serves the whole app.
            _reader = new DatabaseReader(databasePath);
            _logger = logger;
        }

        public Task<GeoLocation?> GetLocationAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var address) || IsPrivateOrLoopback(address))
                return Task.FromResult<GeoLocation?>(null);

            try
            {
                if (!_reader.TryCity(address, out var r) || r == null)
                    return Task.FromResult<GeoLocation?>(null);

                // Without coordinates the travel check can't use it, but the country is still useful.
                var lat = r.Location.Latitude ?? 0;
                var lon = r.Location.Longitude ?? 0;
                var hasCoordinates = r.Location.Latitude.HasValue && r.Location.Longitude.HasValue;
                if (!hasCoordinates && string.IsNullOrEmpty(r.Country.IsoCode))
                    return Task.FromResult<GeoLocation?>(null);

                return Task.FromResult<GeoLocation?>(new GeoLocation(
                    lat, lon,
                    r.City.Name ?? string.Empty,
                    r.Country.Name ?? string.Empty,
                    r.Country.IsoCode));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GeoIP lookup failed for {Ip}.", ipAddress);
                return Task.FromResult<GeoLocation?>(null);
            }
        }

        internal static bool IsPrivateOrLoopback(IPAddress address)
        {
            if (IPAddress.IsLoopback(address)) return true;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;

            var b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] is >= 16 and <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254);
        }

        public void Dispose() => _reader.Dispose();
    }

    /// <summary>
    /// Used when no GeoIP database is installed: every location is unknown, so location-based
    /// checks (impossible travel, country defaults, language by country) are skipped rather than guessed.
    /// </summary>
    public sealed class NullGeoIpService : IGeoIpService
    {
        public Task<GeoLocation?> GetLocationAsync(string ipAddress) => Task.FromResult<GeoLocation?>(null);
    }
}
