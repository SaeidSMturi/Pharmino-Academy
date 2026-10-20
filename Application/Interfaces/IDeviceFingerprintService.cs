using Microsoft.AspNetCore.Http;

namespace Application.Interfaces
{
    /// <summary>
    /// تولید اثر انگشت دستگاه (Device Fingerprint) برای Bind کردن کوکی احراز هویت به یک دستگاه/شبکه مشخص
    /// Generates a lightweight, deterministic device fingerprint (User-Agent + IP address hash) used to bind
    /// the authentication cookie to the exact device/network combination observed at login.
    /// </summary>
    public interface IDeviceFingerprintService
    {
        /// <summary>
        /// تولید اثر انگشت از HttpContext جاری (User-Agent + IP واقعی مشتری)
        /// Builds the fingerprint from the current request's User-Agent header and resolved client IP.
        /// </summary>
        string GenerateFingerprint(HttpContext context);

        /// <summary>
        /// تولید اثر انگشت از مقادیر خام User-Agent و IP (برای مقایسه با مقدار ذخیره‌شده)
        /// Builds the fingerprint from raw User-Agent/IP values (used when re-computing for comparison).
        /// </summary>
        string GenerateFingerprint(string userAgent, string ipAddress);
    }
}
