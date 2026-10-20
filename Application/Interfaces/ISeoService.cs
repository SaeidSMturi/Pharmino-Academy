using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs;
using Domain.Entities.Seo;

namespace Application.Interfaces
{
    public interface ISeoService
    {
        // Redirects Management
        Task<List<SeoRedirect>> GetRedirectsAsync();
        Task<SeoRedirect?> GetRedirectByIdAsync(int id);
        Task<SeoRedirect?> GetRedirectByOldUrlAsync(string oldUrl);
        Task AddRedirectAsync(SeoRedirect redirect);
        Task UpdateRedirectAsync(SeoRedirect redirect);
        Task DeleteRedirectAsync(int id);

        // Technical Errors
        Task LogErrorAsync(string url, int statusCode, string? referrer, string? userAgent);
        Task<List<SeoErrorLog>> GetErrorLogsAsync();
        Task ResolveErrorLogAsync(int id);
        Task DeleteErrorLogAsync(int id);
        Task PurgeBotErrorsAsync();
        Task PurgeAllResolvedErrorsAsync();

        // Crawling & Content Monitoring
        Task<List<SeoPageScan>> GetPageScansAsync();
        Task<List<SeoImage>> GetSeoImagesAsync();
        Task UpdateImageAltAsync(int imageId, string altText);
        Task RunCrawlingAsync(string baseUrl);
        Task<int> CalculateHealthScoreAsync();

        // Advanced On-Page Analyzer
        Task<SeoOnPageResult> AnalyzePageAsync(string url, string? keyword = null);

        // Robots.txt Management
        Task<string> ReadRobotsTxtAsync();
        Task WriteRobotsTxtAsync(string content);

        // Advanced Sitemap Configurations
        Task<List<SeoSitemapSetting>> GetSitemapSettingsAsync();
        Task AddOrUpdateSitemapSettingAsync(SeoSitemapSetting setting);
        Task DeleteSitemapSettingAsync(int id);

        // Dynamic Sitemap Generator
        Task GenerateSitemapsAsync(string baseUrl);

        // General System Checks
        Task<bool> CheckRobotsTxtAsync(string baseUrl);
        Task<bool> CheckSitemapIndexAsync(string baseUrl);
        Task<bool> IsGlobalNoindexActiveAsync();
        Task SetGlobalNoindexAsync(bool noindex);

        // Page-by-Page Manual SEO Metadata Overrides
        Task<List<PageSeo>> GetPageSeosAsync();
        Task<PageSeo?> GetPageSeoByPathAsync(string path);
        Task<PageSeo?> GetPageSeoByIdAsync(int id);
        Task AddOrUpdatePageSeoAsync(PageSeo pageSeo);
        Task DeletePageSeoAsync(int id);
    }
}
