namespace Application.Interfaces
{
    /// <summary>
    /// تجزیه رشته User-Agent برای نمایش نام سیستم‌عامل و مرورگر به کاربر در داشبورد «نشست‌های فعال»
    /// </summary>
    public interface IUserAgentParserService
    {
        /// <summary>تجزیه User-Agent خام و بازگرداندن نام سیستم‌عامل و مرورگر / Parses a raw User-Agent string into (OperatingSystem, Browser) display names.</summary>
        (string OperatingSystem, string Browser) Parse(string? userAgent);
    }
}
