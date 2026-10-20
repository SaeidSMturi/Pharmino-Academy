namespace Infrastructure.Services
{
    /// <summary>
    /// نام‌های اسکیم‌های احراز هویت سفارشی استفاده‌شده در برنامه
    /// Well-known custom authentication scheme names used across the application.
    /// </summary>
    public static class AuthenticationSchemes
    {
        /// <summary>
        /// اسکیم کوکی موقت برای مرحله میانی ورود خارجی (گوگل) - پیش از صدور کوکی اصلی با SessionId
        /// Short-lived cookie scheme used only during the external (Google) login handshake, before the
        /// main SessionId-bearing authentication cookie is issued by <c>AccountController.GoogleResponse</c>.
        /// </summary>
        public const string ExternalAuthenticationScheme = "ExternalAuth";
    }
}
