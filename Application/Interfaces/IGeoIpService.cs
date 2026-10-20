namespace Application.Interfaces
{
    /// <summary>
    /// نتیجه یک جستجوی GeoIP: مختصات جغرافیایی تخمینی برای یک آدرس IP
    /// Result of a GeoIP lookup: the approximate geographic coordinates for an IP address.
    /// </summary>
    public record GeoLocation(double Latitude, double Longitude, string City, string Country);

    /// <summary>
    /// سرویس تعیین موقعیت جغرافیایی از روی IP - رابط قابل جایگزینی با سرویس‌های واقعی
    /// </summary>
    public interface IGeoIpService
    {
        Task<GeoLocation> GetLocationAsync(string ipAddress);
    }
}
