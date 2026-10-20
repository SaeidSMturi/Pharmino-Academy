using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Domain.Entities.Seo;

namespace Application.Services
{
    public partial class SeoService
    {
        public async Task<List<SeoSitemapSetting>> GetSitemapSettingsAsync()
        {
            return await _context.SeoSitemapSettings
                .OrderBy(s => s.IsCustomLink)
                .ThenBy(s => s.IsExcludedPath)
                .ThenBy(s => s.Key)
                .ToListAsync();
        }

        public async Task AddOrUpdateSitemapSettingAsync(SeoSitemapSetting setting)
        {
            if (setting.Id > 0)
            {
                _context.SeoSitemapSettings.Update(setting);
            }
            else
            {
                _context.SeoSitemapSettings.Add(setting);
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteSitemapSettingAsync(int id)
        {
            var setting = await _context.SeoSitemapSettings.FindAsync(id);
            if (setting != null)
            {
                _context.SeoSitemapSettings.Remove(setting);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<string> ReadRobotsTxtAsync()
        {
            var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var robotsPath = Path.Combine(wwwroot, "robots.txt");
            
            if (File.Exists(robotsPath))
            {
                return await File.ReadAllTextAsync(robotsPath, Encoding.UTF8);
            }

            // Return default template
            return "User-agent: *\nAllow: /\n\nSitemap: /sitemap_index.xml\n";
        }

        public async Task WriteRobotsTxtAsync(string content)
        {
            var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            if (!Directory.Exists(wwwroot))
            {
                Directory.CreateDirectory(wwwroot);
            }
            var robotsPath = Path.Combine(wwwroot, "robots.txt");
            await File.WriteAllTextAsync(robotsPath, content, Encoding.UTF8);
        }

        public async Task GenerateSitemapsAsync(string baseUrl)
        {
            var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            if (!Directory.Exists(wwwroot))
            {
                Directory.CreateDirectory(wwwroot);
            }

            var cleanBaseUrl = baseUrl.TrimEnd('/');
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            // Load all custom sitemap settings
            var settings = await _context.SeoSitemapSettings.ToListAsync();
            
            // Map settings configurations
            var postsPriority = settings.FirstOrDefault(s => s.Key == "Section.Posts.Priority")?.Value ?? "0.8";
            var postsChangeFreq = settings.FirstOrDefault(s => s.Key == "Section.Posts.ChangeFreq")?.Value ?? "weekly";
            
            var pagesPriority = settings.FirstOrDefault(s => s.Key == "Section.Pages.Priority")?.Value ?? "1.0";
            var pagesChangeFreq = settings.FirstOrDefault(s => s.Key == "Section.Pages.ChangeFreq")?.Value ?? "daily";

            var productsPriority = settings.FirstOrDefault(s => s.Key == "Section.Products.Priority")?.Value ?? "0.9";
            var productsChangeFreq = settings.FirstOrDefault(s => s.Key == "Section.Products.ChangeFreq")?.Value ?? "weekly";

            var categoriesPriority = settings.FirstOrDefault(s => s.Key == "Section.Categories.Priority")?.Value ?? "0.6";
            var categoriesChangeFreq = settings.FirstOrDefault(s => s.Key == "Section.Categories.ChangeFreq")?.Value ?? "weekly";

            // Excluded paths pattern
            var excludedPaths = settings
                .Where(s => s.IsExcludedPath && !string.IsNullOrWhiteSpace(s.Value))
                .Select(s => s.Value!.ToLower().Trim())
                .ToList();

            // Custom links to include
            var customLinks = settings
                .Where(s => s.IsCustomLink && !string.IsNullOrWhiteSpace(s.Value))
                .ToList();

            // 1. Generate sitemap_posts.xml
            var posts = await _context.Posts
                .Where(p => p.IsPublish)
                .OrderByDescending(p => p.CreateDate)
                .ToListAsync();

            var postsFiltered = posts.Where(p => !IsPathExcluded($"/Post/Details/{p.PostId}", excludedPaths)).ToList();

            var postsDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "urlset",
                    postsFiltered.Select(p => new XElement(ns + "url",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/Post/Details/{p.PostId}"),
                        new XElement(ns + "lastmod", p.CreateDate.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
                        new XElement(ns + "changefreq", postsChangeFreq),
                        new XElement(ns + "priority", postsPriority)
                    ))
                )
            );
            await File.WriteAllTextAsync(Path.Combine(wwwroot, "sitemap_posts.xml"), postsDoc.ToString(), Encoding.UTF8);

            // 2. Generate sitemap_pages.xml
            var staticPages = new List<(string Path, string Frequency, double Priority)>
            {
                ("/", pagesChangeFreq, double.Parse(pagesPriority)),
                ("/Home/Index", pagesChangeFreq, double.Parse(pagesPriority) * 0.9),
                ("/Privacy", "monthly", 0.3)
            };

            var staticPagesFiltered = staticPages.Where(sp => !IsPathExcluded(sp.Path, excludedPaths)).ToList();

            var pagesDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "urlset",
                    staticPagesFiltered.Select(sp => new XElement(ns + "url",
                        new XElement(ns + "loc", $"{cleanBaseUrl}{sp.Path}"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                        new XElement(ns + "changefreq", sp.Frequency),
                        new XElement(ns + "priority", sp.Priority.ToString("0.0"))
                    ))
                )
            );
            await File.WriteAllTextAsync(Path.Combine(wwwroot, "sitemap_pages.xml"), pagesDoc.ToString(), Encoding.UTF8);

            // 3. Generate sitemap_products.xml
            var courses = await _context.Courses
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreateDate)
                .ToListAsync();

            var coursesFiltered = courses.Where(c => !IsPathExcluded($"/Course/{c.CourseId}", excludedPaths)).ToList();

            var productsDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "urlset",
                    coursesFiltered.Select(c => new XElement(ns + "url",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/Course/{c.CourseId}"),
                        new XElement(ns + "lastmod", (c.LastUpdate ?? c.CreateDate).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
                        new XElement(ns + "changefreq", productsChangeFreq),
                        new XElement(ns + "priority", productsPriority)
                    ))
                )
            );
            await File.WriteAllTextAsync(Path.Combine(wwwroot, "sitemap_products.xml"), productsDoc.ToString(), Encoding.UTF8);

            // 4. Generate sitemap_categories.xml
            var tagsList = posts.Where(p => !string.IsNullOrEmpty(p.Tags))
                .SelectMany(p => p.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim().ToLower())
                .Distinct()
                .ToList();

            if (!tagsList.Any())
            {
                tagsList.AddRange(new[] { "general", "medication", "pharmacy", "health" });
            }

            var tagsFiltered = tagsList.Where(tag => !IsPathExcluded($"/Category/{tag}", excludedPaths)).ToList();

            var categoriesDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "urlset",
                    tagsFiltered.Select(tag => new XElement(ns + "url",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/Category/{Uri.EscapeDataString(tag)}"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                        new XElement(ns + "changefreq", categoriesChangeFreq),
                        new XElement(ns + "priority", categoriesPriority)
                    ))
                )
            );
            await File.WriteAllTextAsync(Path.Combine(wwwroot, "sitemap_categories.xml"), categoriesDoc.ToString(), Encoding.UTF8);

            // 5. Generate sitemap_custom.xml (for custom links added by user)
            var customFiltered = customLinks.Where(cl => !IsPathExcluded(cl.Value!, excludedPaths)).ToList();
            
            var customDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "urlset",
                    customFiltered.Select(cl => new XElement(ns + "url",
                        new XElement(ns + "loc", cl.Value!.StartsWith("http") ? cl.Value : $"{cleanBaseUrl}{cl.Value}"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                        new XElement(ns + "changefreq", cl.CustomChangeFreq ?? "weekly"),
                        new XElement(ns + "priority", (cl.CustomPriority ?? 0.5).ToString("0.0"))
                    ))
                )
            );
            await File.WriteAllTextAsync(Path.Combine(wwwroot, "sitemap_custom.xml"), customDoc.ToString(), Encoding.UTF8);

            // 6. Generate sitemap_index.xml (include custom sitemap if it has items)
            var indexDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(ns + "sitemapindex",
                    new XElement(ns + "sitemap",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/sitemap_posts.xml"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    ),
                    new XElement(ns + "sitemap",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/sitemap_pages.xml"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    ),
                    new XElement(ns + "sitemap",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/sitemap_products.xml"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    ),
                    new XElement(ns + "sitemap",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/sitemap_categories.xml"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    ),
                    new XElement(ns + "sitemap",
                        new XElement(ns + "loc", $"{cleanBaseUrl}/sitemap_custom.xml"),
                        new XElement(ns + "lastmod", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    )
                )
            );
            await File.WriteAllTextAsync(Path.Combine(wwwroot, "sitemap_index.xml"), indexDoc.ToString(), Encoding.UTF8);

            // Update robots.txt to reference index sitemap if not exists
            var robotsPath = Path.Combine(wwwroot, "robots.txt");
            string robotsContent = $"User-agent: *\nAllow: /\n\nSitemap: {cleanBaseUrl}/sitemap_index.xml\n";
            if (!File.Exists(robotsPath))
            {
                await File.WriteAllTextAsync(robotsPath, robotsContent, Encoding.UTF8);
            }
        }

        private bool IsPathExcluded(string path, List<string> excludedPaths)
        {
            path = path.ToLower().Trim();
            foreach (var pattern in excludedPaths)
            {
                if (pattern.EndsWith("*"))
                {
                    var prefix = pattern.Substring(0, pattern.Length - 1);
                    if (path.StartsWith(prefix)) return true;
                }
                else
                {
                    if (path == pattern) return true;
                }
            }
            return false;
        }

        public async Task<bool> CheckRobotsTxtAsync(string baseUrl)
        {
            try
            {
                var cleanBase = baseUrl.TrimEnd('/');
                var response = await _httpClient.GetAsync($"{cleanBase}/robots.txt");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                return File.Exists(Path.Combine(wwwroot, "robots.txt"));
            }
        }

        public async Task<bool> CheckSitemapIndexAsync(string baseUrl)
        {
            try
            {
                var cleanBase = baseUrl.TrimEnd('/');
                var response = await _httpClient.GetAsync($"{cleanBase}/sitemap_index.xml");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                return File.Exists(Path.Combine(wwwroot, "sitemap_index.xml"));
            }
        }
    }
}
