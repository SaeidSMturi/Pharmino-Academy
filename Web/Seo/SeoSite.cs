using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Web.Seo
{
    /// <summary>
    /// One place for the site's search-engine identity: brand, public base URL and the per-language URLs.
    /// Languages are addressed with ?culture=xx (English has no parameter), which the
    /// SmartRequestCultureProvider already honours, so every language has its own crawlable URL.
    /// </summary>
    public static class SeoSite
    {
        public static readonly string[] Cultures = { "en", "ar", "fa" };

        /// <summary>Extra hreflang codes that point at the same English / Arabic pages, to signal the target regions.</summary>
        public static readonly string[] RegionalEnglish = { "en-IN", "en-PK", "en-AE", "en-SA", "en-OM", "en-QA", "en-KW", "en-BH", "en-EG" };
        public static readonly string[] RegionalArabic = { "ar-EG", "ar-SA", "ar-AE", "ar-OM", "ar-QA", "ar-KW", "ar-BH", "ar-JO", "ar-IQ" };

        /// <summary>Countries the academy serves (schema.org areaServed).</summary>
        public static readonly string[] AreaServed = { "Oman", "Saudi Arabia", "United Arab Emirates", "Qatar", "Kuwait", "Bahrain", "Egypt", "Jordan", "Iraq", "India", "Pakistan" };

        public static string BrandName(string culture) => Lang(culture) switch
        {
            "ar" => "أكاديمية فارمينو",
            "fa" => "آکادمی فارمینو",
            _ => "Pharmino Academy"
        };

        public static string Lang(string? culture)
        {
            if (string.IsNullOrEmpty(culture)) return "en";
            var two = culture.Length >= 2 ? culture[..2].ToLowerInvariant() : culture.ToLowerInvariant();
            return Array.IndexOf(Cultures, two) >= 0 ? two : "en";
        }

        public static string CurrentLang => Lang(CultureInfo.CurrentUICulture.Name);

        /// <summary>Public base URL without a trailing slash: Seo:BaseUrl, then Smtp:SiteBaseUrl, then the request.</summary>
        public static string BaseUrl(HttpContext context)
        {
            var config = context.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
            var configured = config?["Seo:BaseUrl"];
            if (string.IsNullOrWhiteSpace(configured)) configured = config?["Smtp:SiteBaseUrl"];
            if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');
            return $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}".TrimEnd('/');
        }

        /// <summary>Absolute URL of <paramref name="path"/> in a language: English has no parameter, others get ?culture=xx.</summary>
        public static string LocalizedUrl(string baseUrl, string path, string lang)
        {
            if (string.IsNullOrEmpty(path)) path = "/";
            if (!path.StartsWith('/')) path = "/" + path;
            var url = baseUrl.TrimEnd('/') + path;
            return lang == "en" ? url : $"{url}?culture={lang}";
        }

        public static string OgLocale(string lang) => lang switch
        {
            "ar" => "ar_AR",
            "fa" => "fa_IR",
            _ => "en_US"
        };

        /// <summary>
        /// Keeps a meta description within the 160 characters search engines show, cutting at a word boundary.
        /// </summary>
        public static string FitDescription(string? text, int max = 160)
        {
            var clean = (text ?? string.Empty).Trim();
            if (clean.Length <= max) return clean;
            var cut = clean.LastIndexOf(' ', max - 1);
            if (cut < max / 2) cut = max - 1;
            return clean.Substring(0, cut).TrimEnd(' ', ',', '.', ':', ';', '،', '-') + "…";
        }

        /// <summary>Paths that must not be indexed (sign-in, checkout and other utility pages).</summary>
        public static bool IsNoIndexPath(string path)
        {
            string[] prefixes = { "/Account", "/Payment", "/Signup", "/Language", "/SampleAuth", "/Admin", "/Students", "/User", "/api" };
            return prefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }
    }
}
