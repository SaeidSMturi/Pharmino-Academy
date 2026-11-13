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


        /// <summary>
        /// One canonical form of a request path, so every URL variant of a page (/course/5/, /Home/Index, /courses, /?utm=…)
        /// maps to the same address that the sitemap lists: no trailing slash, no /Home or /Index, each segment capitalised.
        /// </summary>
        public static string CanonicalPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "/";
            var q = path.IndexOfAny(new[] { '?', '#' });
            if (q >= 0) path = path[..q];
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count > 0 && parts[^1].Equals("Index", StringComparison.OrdinalIgnoreCase)) parts.RemoveAt(parts.Count - 1);
            if (parts.Count == 1 && parts[0].Equals("Home", StringComparison.OrdinalIgnoreCase)) parts.Clear();
            if (parts.Count == 0) return "/";
            for (var i = 0; i < parts.Count; i++)
            {
                var seg = parts[i];
                if (seg.Length > 0 && char.IsLower(seg[0])) parts[i] = char.ToUpperInvariant(seg[0]) + seg[1..];
            }
            return "/" + string.Join('/', parts);
        }

        /// <summary>Absolute, query-free canonical URL of the current request in the current language.</summary>
        public static string CanonicalUrl(HttpContext context, string? lang = null)
            => LocalizedUrl(BaseUrl(context), CanonicalPath(context.Request.Path.Value), lang ?? CurrentLang);

        /// <summary>Makes an admin-entered canonical absolute (Google ignores relative or malformed ones); falls back when unusable.</summary>
        public static string ResolveCanonical(string baseUrl, string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            value = value.Trim();
            if (value.StartsWith('/') && !value.StartsWith("//")) return baseUrl.TrimEnd('/') + value;
            return Uri.TryCreate(value, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp) ? value : fallback;
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
