namespace Application.Interfaces
{
    /// <summary>
    /// Result of a GeoIP lookup: the approximate location of an IP address.
    /// <paramref name="CountryCode"/> is the ISO 3166-1 alpha-2 code (e.g. "OM").
    /// </summary>
    public record GeoLocation(double Latitude, double Longitude, string City, string Country, string? CountryCode = null);

    /// <summary>
    /// Looks up where an IP address is. Returns null when the location is unknown
    /// (private/loopback addresses, addresses missing from the database, or no database installed),
    /// so callers can skip location-based logic instead of acting on a guess.
    /// </summary>
    public interface IGeoIpService
    {
        Task<GeoLocation?> GetLocationAsync(string ipAddress);
    }
}
