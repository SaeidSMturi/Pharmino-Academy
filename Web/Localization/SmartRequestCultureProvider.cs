using System.Security.Claims;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Localization;
using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Localization
{
    public class SmartRequestCultureProvider : RequestCultureProvider
    {
        private static readonly HashSet<string> ArabicCountries = new(StringComparer.OrdinalIgnoreCase)
        {
            // Country Codes
            "AE", "BH", "DZ", "EG", "IQ", "JO", "KM", "KW", "LB", "LY", "MA", "MR", "OM", "PS", "QA", "SA", "SD", "SO", "SY", "TN", "YE", "DJ",
            // Country Names
            "United Arab Emirates", "Bahrain", "Algeria", "Egypt", "Iraq", "Jordan", "Comoros", "Kuwait", "Lebanon", "Libya", "Morocco", "Mauritania", "Oman", "Palestine", "Qatar", "Saudi Arabia", "Sudan", "Somalia", "Syria", "Tunisia", "Yemen", "Djibouti"
        };

        private static readonly string[] SupportedCultures = new[] { "en", "ar" };

        public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            // 1. Query String Detection
            var queryCulture = httpContext.Request.Query["culture"].ToString();
            if (!string.IsNullOrEmpty(queryCulture) && IsSupported(queryCulture))
            {
                SaveCultureInCookie(httpContext, queryCulture);
                await SaveCultureInDbForUserAsync(httpContext, queryCulture);
                return new ProviderCultureResult(queryCulture);
            }

            // 2. Cookie Detection
            var cookieValue = httpContext.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
            if (!string.IsNullOrEmpty(cookieValue))
            {
                var parsedCookie = CookieRequestCultureProvider.ParseCookieValue(cookieValue);
                if (parsedCookie != null && parsedCookie.Cultures.Count > 0)
                {
                    var culture = parsedCookie.Cultures[0].Value;
                    if (IsSupported(culture))
                    {
                        return new ProviderCultureResult(culture);
                    }
                }
            }

            // 3. User Claims / DB Preference Detection (for Authenticated Users)
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                // Check claim first (extremely fast)
                var preferredLanguageClaim = httpContext.User.FindFirst("PreferredLanguage")?.Value;
                if (!string.IsNullOrEmpty(preferredLanguageClaim) && IsSupported(preferredLanguageClaim))
                {
                    SaveCultureInCookie(httpContext, preferredLanguageClaim); // Keep cookie in sync
                    return new ProviderCultureResult(preferredLanguageClaim);
                }

                // If claim not found, query the Database
                var userIdStr = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var dbContext = httpContext.RequestServices.GetService<WebContext>();
                    if (dbContext != null)
                    {
                        var user = await dbContext.Users.FindAsync(userId);
                        if (user != null && !string.IsNullOrEmpty(user.PreferredLanguage) && IsSupported(user.PreferredLanguage))
                        {
                            SaveCultureInCookie(httpContext, user.PreferredLanguage);
                            return new ProviderCultureResult(user.PreferredLanguage);
                        }
                    }
                }
            }

            // 4. GeoIP Detection
            string detectedCulture = null;
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(ipAddress) && ipAddress != "::1" && ipAddress != "127.0.0.1")
            {
                var geoIpService = httpContext.RequestServices.GetService<IGeoIpService>();
                if (geoIpService != null)
                {
                    try
                    {
                        var location = await geoIpService.GetLocationAsync(ipAddress);
                        if (location != null && !string.IsNullOrEmpty(location.Country))
                        {
                            if (ArabicCountries.Contains(location.Country))
                            {
                                detectedCulture = "ar";
                            }
                        }
                    }
                    catch
                    {
                        // Safe failover
                    }
                }
            }

            // 5. Browser Accept-Language Header Matching
            if (string.IsNullOrEmpty(detectedCulture))
            {
                var acceptLanguages = httpContext.Request.GetTypedHeaders().AcceptLanguage;
                if (acceptLanguages != null)
                {
                    var matchedCulture = acceptLanguages
                        .OrderByDescending(x => x.Quality ?? 1.0)
                        .Select(x => x.Value.Value.Split('-')[0])
                        .FirstOrDefault(lang => IsSupported(lang));

                    if (matchedCulture != null)
                    {
                        detectedCulture = matchedCulture;
                    }
                }
            }

            // 6. Final Default Fallback (English)
            if (string.IsNullOrEmpty(detectedCulture))
            {
                detectedCulture = "en";
            }

            // Save in cookie and database for performance on subsequent requests
            SaveCultureInCookie(httpContext, detectedCulture);
            await SaveCultureInDbForUserAsync(httpContext, detectedCulture);

            return new ProviderCultureResult(detectedCulture);
        }

        private static bool IsSupported(string culture)
        {
            return SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase);
        }

        private static void SaveCultureInCookie(HttpContext httpContext, string culture)
        {
            if (httpContext.Response.HasStarted) return;

            var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
            httpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                }
            );
        }

        private static async Task SaveCultureInDbForUserAsync(HttpContext httpContext, string culture)
        {
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var userIdStr = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var dbContext = httpContext.RequestServices.GetService<WebContext>();
                    if (dbContext != null)
                    {
                        var user = await dbContext.Users.FindAsync(userId);
                        if (user != null && user.PreferredLanguage != culture)
                        {
                            user.PreferredLanguage = culture;
                            await dbContext.SaveChangesAsync();
                        }
                    }
                }
            }
        }
    }
}
