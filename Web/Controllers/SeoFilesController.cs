using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Seo;

namespace Web.Controllers
{
    /// <summary>
    /// Serves robots.txt and the XML sitemap live, always with the public domain and current courses.
    /// Program.cs rewrites /robots.txt, /sitemap.xml, /sitemap_index.xml and the old /sitemap_*.xml files here
    /// before static files run, so a stale copy in wwwroot can never be served again.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public class SeoFilesController : Controller
    {
        private readonly WebContext _context;
        private readonly ISeoService _seoService;

        public SeoFilesController(WebContext context, ISeoService seoService)
        {
            _context = context;
            _seoService = seoService;
        }

        [HttpGet("seo-files/robots")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> Robots()
        {
            var baseUrl = SeoSite.BaseUrl(HttpContext);
            var content = await _seoService.ReadRobotsTxtAsync();

            // Whatever the admin wrote, the Sitemap line must point at this domain.
            content = Regex.Replace(content, @"^\s*Sitemap:.*$\r?\n?", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase).TrimEnd();
            content += $"\n\nSitemap: {baseUrl}/sitemap.xml\n";
            return Content(content, "text/plain", Encoding.UTF8);
        }

        [HttpGet("seo-files/sitemap")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = SeoSite.BaseUrl(HttpContext);
            var settings = await _seoService.GetSitemapSettingsAsync();

            var pages = new List<(string Path, DateTime LastMod, string ChangeFreq, string Priority)>
            {
                ("/", DateTime.UtcNow.Date, "daily", "1.0"),
                ("/Courses", DateTime.UtcNow.Date, "daily", "0.9"),
                ("/Home/Privacy", new DateTime(2026, 11, 1), "yearly", "0.2")
            };

            var courses = await _context.Courses
                .AsNoTracking()
                .Where(c => c.IsActive && c.IsListedForSale)
                .OrderByDescending(c => c.CreateDate)
                .Select(c => new { c.CourseId, c.CreateDate, c.LastUpdate })
                .ToListAsync();
            pages.AddRange(courses.Select(c => ($"/Course/{c.CourseId}", (c.LastUpdate ?? c.CreateDate).ToUniversalTime(), "weekly", "0.8")));

            // Custom links added in Admin > SEO > Sitemap settings (same-site paths only).
            foreach (var link in settings.Where(s => s.IsCustomLink && !string.IsNullOrWhiteSpace(s.Value)))
            {
                var value = link.Value!.Trim();
                if (value.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)) value = value[baseUrl.Length..];
                if (!value.StartsWith('/')) continue;
                pages.Add((value, DateTime.UtcNow.Date, link.CustomChangeFreq ?? "weekly", (link.CustomPriority ?? 0.5).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            }

            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XNamespace xhtml = "http://www.w3.org/1999/xhtml";
            var urlset = new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", xhtml));

            foreach (var page in pages.Where(p => !_seoService.IsSitemapPathExcluded(p.Path, settings)).DistinctBy(p => p.Path.ToLowerInvariant()))
            {
                // One <url> per language, each listing all language versions (Google's recommended hreflang sitemap form).
                foreach (var lang in SeoSite.Cultures)
                {
                    var url = new XElement(ns + "url",
                        new XElement(ns + "loc", SeoSite.LocalizedUrl(baseUrl, page.Path, lang)),
                        new XElement(ns + "lastmod", page.LastMod.ToString("yyyy-MM-dd")),
                        new XElement(ns + "changefreq", page.ChangeFreq),
                        new XElement(ns + "priority", page.Priority));
                    foreach (var alt in SeoSite.Cultures)
                    {
                        url.Add(new XElement(xhtml + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", alt),
                            new XAttribute("href", SeoSite.LocalizedUrl(baseUrl, page.Path, alt))));
                    }
                    url.Add(new XElement(xhtml + "link",
                        new XAttribute("rel", "alternate"),
                        new XAttribute("hreflang", "x-default"),
                        new XAttribute("href", SeoSite.LocalizedUrl(baseUrl, page.Path, "en"))));
                    urlset.Add(url);
                }
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
            var xml = doc.Declaration + "\n" + doc.Root!.ToString(SaveOptions.DisableFormatting);
            return Content(xml, "application/xml", Encoding.UTF8);
        }
    }
}
