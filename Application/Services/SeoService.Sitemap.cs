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

        /// <summary>
        /// Admin-edited robots.txt lives outside wwwroot (AppData/robots.txt) so deployments don't overwrite it;
        /// it is served by /robots.txt through SeoFilesController, which also fixes the Sitemap line.
        /// </summary>
        private string RobotsTxtPath => Path.Combine(_env.ContentRootPath, "AppData", "robots.txt");

        public async Task<string> ReadRobotsTxtAsync()
        {
            if (File.Exists(RobotsTxtPath))
            {
                return await File.ReadAllTextAsync(RobotsTxtPath, Encoding.UTF8);
            }
            return DefaultRobotsTxt;
        }

        public const string DefaultRobotsTxt =
            "User-agent: *\n" +
            "Allow: /\n" +
            "Disallow: /Admin/\n" +
            "Disallow: /Students/\n" +
            "Disallow: /User/\n" +
            "Disallow: /api/\n" +
            "Disallow: /Payment/\n" +
            "Disallow: /Language/\n" +
            "Disallow: /SampleAuth/\n" +
            "Disallow: /resize\n" +
            "Disallow: /MigrateAllToHls\n";

        public async Task WriteRobotsTxtAsync(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RobotsTxtPath)!);
            await File.WriteAllTextAsync(RobotsTxtPath, content, Encoding.UTF8);
        }

        /// <summary>
        /// Sitemaps are now built live on every request (/sitemap.xml, see SeoFilesController), so they always use
        /// the public domain and current courses. This only removes old static copies from wwwroot.
        /// </summary>
        public Task GenerateSitemapsAsync(string baseUrl)
        {
            var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            foreach (var name in new[] { "sitemap_index.xml", "sitemap_posts.xml", "sitemap_pages.xml", "sitemap_products.xml", "sitemap_categories.xml", "sitemap_custom.xml", "sitemap.xml", "robots.txt" })
            {
                var file = Path.Combine(wwwroot, name);
                if (File.Exists(file)) File.Delete(file);
            }
            return Task.CompletedTask;
        }

        /// <summary>True when a sitemap path is excluded by an Admin sitemap setting (supports a trailing *).</summary>
        public bool IsSitemapPathExcluded(string path, IEnumerable<SeoSitemapSetting> settings)
        {
            var excluded = settings
                .Where(s => s.IsExcludedPath && !string.IsNullOrWhiteSpace(s.Value))
                .Select(s => s.Value!.ToLower().Trim())
                .ToList();
            return IsPathExcluded(path, excluded);
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
