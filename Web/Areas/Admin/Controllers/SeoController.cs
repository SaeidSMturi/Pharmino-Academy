using System;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.DTOs;
using Domain.Entities.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Web.Areas.Admin.ViewModels;

namespace Web.Areas.Admin.Controllers
{
    public class SeoController : AdminBaseController
    {
        private readonly ISeoService _seoService;
        private readonly ILogger<SeoController> _logger;

        public SeoController(ISeoService seoService, ILogger<SeoController> logger)
        {
            _seoService = seoService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            
            var viewModel = new SeoDashboardViewModel
            {
                HealthScore = await _seoService.CalculateHealthScoreAsync(),
                IsGlobalNoindexActive = await _seoService.IsGlobalNoindexActiveAsync(),
                HasRobotsTxt = await _seoService.CheckRobotsTxtAsync(baseUrl),
                HasSitemapIndex = await _seoService.CheckSitemapIndexAsync(baseUrl),
                PageScans = await _seoService.GetPageScansAsync(),
                MissingAltImages = await _seoService.GetSeoImagesAsync(),
                CriticalErrors = await _seoService.GetErrorLogsAsync(),
                Redirects = await _seoService.GetRedirectsAsync()
            };

            return View(viewModel);
        }

        #region Redirects Management

        [HttpGet]
        public async Task<IActionResult> Redirects(string? oldUrl = null)
        {
            var redirects = await _seoService.GetRedirectsAsync();
            ViewBag.PreFilledOldUrl = oldUrl;
            return View(redirects);
        }

        [HttpPost]
        public async Task<IActionResult> AddRedirect(AddRedirectViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.OldUrl) || string.IsNullOrWhiteSpace(model.NewUrl))
            {
                TempData["ErrorMessage"] = "Both Old URL and New URL are required.";
                return RedirectToAction(nameof(Redirects));
            }

            var redirect = new SeoRedirect
            {
                OldUrl = model.OldUrl,
                NewUrl = model.NewUrl,
                IsPermanent = model.IsPermanent,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _seoService.AddRedirectAsync(redirect);
            TempData["SuccessMessage"] = "Redirect rule added successfully.";
            return RedirectToAction(nameof(Redirects));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRedirect(int id)
        {
            await _seoService.DeleteRedirectAsync(id);
            TempData["SuccessMessage"] = "Redirect rule deleted successfully.";
            return RedirectToAction(nameof(Redirects));
        }

        #endregion

        #region Technical Errors Monitoring

        public async Task<IActionResult> Errors()
        {
            var errors = await _seoService.GetErrorLogsAsync();
            ViewBag.PageScans = await _seoService.GetPageScansAsync();
            ViewBag.MissingAlts = await _seoService.GetSeoImagesAsync();
            return View(errors);
        }

        [HttpPost]
        public async Task<IActionResult> ResolveError(int id)
        {
            await _seoService.ResolveErrorLogAsync(id);
            TempData["SuccessMessage"] = "Error marked as resolved.";
            return RedirectToAction(nameof(Errors));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteError(int id)
        {
            await _seoService.DeleteErrorLogAsync(id);
            TempData["SuccessMessage"] = "Error log deleted successfully.";
            return RedirectToAction(nameof(Errors));
        }

        [HttpPost]
        public async Task<IActionResult> PurgeBotErrors()
        {
            await _seoService.PurgeBotErrorsAsync();
            TempData["SuccessMessage"] = "تمام ارورها و اسکن‌های ربات‌ها با موفقیت پاک‌سازی شدند.";
            return RedirectToAction(nameof(Errors));
        }

        [HttpPost]
        public async Task<IActionResult> PurgeResolvedErrors()
        {
            await _seoService.PurgeAllResolvedErrorsAsync();
            TempData["SuccessMessage"] = "آرشیو ارورهای حل‌شده پاک‌سازی گردید.";
            return RedirectToAction(nameof(Errors));
        }

        #endregion

        #region Image Alt Optimizer

        public async Task<IActionResult> ImageAlts()
        {
            var images = await _seoService.GetSeoImagesAsync();
            return View(images);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateImageAlt(int id, string altText)
        {
            try
            {
                await _seoService.UpdateImageAltAsync(id, altText);
                return Json(new { success = true, message = "Image Alt Text updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating image alt text for image ID {ImageId}", id);
                return Json(new { success = false, message = $"Failed to update image: {ex.Message}" });
            }
        }

        #endregion

        #region SEO Action Triggers (AJAX)

        [HttpPost]
        public async Task<IActionResult> TriggerCrawl()
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
                _logger.LogInformation("Starting SEO Crawling from Base URL: {BaseUrl}", baseUrl);
                
                await _seoService.RunCrawlingAsync(baseUrl);
                
                return Json(new { success = true, message = "Crawl finished successfully. Data is updated." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during SEO crawler run.");
                return Json(new { success = false, message = $"Crawler failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateSitemaps()
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
                _logger.LogInformation("Starting Sitemap Index generation for base URL: {BaseUrl}", baseUrl);
                
                await _seoService.GenerateSitemapsAsync(baseUrl);
                
                return Json(new { success = true, message = "Sitemap index and parts generated successfully inside wwwroot." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sitemaps.");
                return Json(new { success = false, message = $"Sitemap generation failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleGlobalNoindex(bool noindex)
        {
            try
            {
                await _seoService.SetGlobalNoindexAsync(noindex);
                var status = noindex ? "enabled" : "disabled";
                return Json(new { success = true, message = $"Global noindex configuration has been {status}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling global noindex.");
                return Json(new { success = false, message = $"Configuration failed: {ex.Message}" });
            }
        }

        #endregion

        #region Technical Actions (On-Page Audit, Robots.txt, Sitemap settings)

        [HttpGet]
        public async Task<IActionResult> Analyzer(string? url, string? keyword)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return View(null);
            }

            var absoluteUrl = url;
            if (url.StartsWith("/"))
            {
                absoluteUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{url}";
            }

            ViewBag.Keyword = keyword;
            ViewBag.AnalyzedUrl = url;

            var result = await _seoService.AnalyzePageAsync(absoluteUrl, keyword);
            return View(result);
        }

        [HttpGet]
        public async Task<IActionResult> RobotsTxt()
        {
            var content = await _seoService.ReadRobotsTxtAsync();
            return View(model: content);
        }

        [HttpPost]
        public async Task<IActionResult> SaveRobotsTxt(string content)
        {
            try
            {
                await _seoService.WriteRobotsTxtAsync(content);
                TempData["SuccessMessage"] = "Robots.txt updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to save Robots.txt: {ex.Message}";
            }
            return RedirectToAction(nameof(RobotsTxt));
        }

        [HttpGet]
        public async Task<IActionResult> SitemapSettings()
        {
            var settings = await _seoService.GetSitemapSettingsAsync();
            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> AddSitemapSetting(string key, string? value, double? customPriority, string? customChangeFreq, bool isCustomLink, bool isExcludedPath)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                TempData["ErrorMessage"] = "Sitemap setting key is required.";
                return RedirectToAction(nameof(SitemapSettings));
            }

            var setting = new SeoSitemapSetting
            {
                Key = key,
                Value = value,
                CustomPriority = customPriority,
                CustomChangeFreq = customChangeFreq,
                IsCustomLink = isCustomLink,
                IsExcludedPath = isExcludedPath,
                UpdatedAt = DateTime.UtcNow
            };

            await _seoService.AddOrUpdateSitemapSettingAsync(setting);
            TempData["SuccessMessage"] = "Sitemap setting added or updated successfully.";
            return RedirectToAction(nameof(SitemapSettings));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSitemapSetting(int id)
        {
            await _seoService.DeleteSitemapSettingAsync(id);
            TempData["SuccessMessage"] = "Sitemap setting deleted successfully.";
            return RedirectToAction(nameof(SitemapSettings));
        }

        #endregion

        #region Page-by-Page SEO Metadata Management

        [HttpGet]
        public async Task<IActionResult> PageSeos()
        {
            var pages = await _seoService.GetPageSeosAsync();
            return View(pages);
        }

        [HttpPost]
        public async Task<IActionResult> SavePageSeo(PageSeo model)
        {
            if (string.IsNullOrWhiteSpace(model.PagePath))
            {
                TempData["ErrorMessage"] = "مسیر صفحه (URL Path) الزامی است.";
                return RedirectToAction(nameof(PageSeos));
            }

            try
            {
                await _seoService.AddOrUpdatePageSeoAsync(model);
                TempData["SuccessMessage"] = "اطلاعات سئو صفحه با موفقیت ذخیره گردید.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ذخیره سئو صفحه {PagePath}", model.PagePath);
                TempData["ErrorMessage"] = $"خطا در ذخیره سئو صفحه: {ex.Message}";
            }

            return RedirectToAction(nameof(PageSeos));
        }

        [HttpPost]
        public async Task<IActionResult> DeletePageSeo(int id)
        {
            try
            {
                await _seoService.DeletePageSeoAsync(id);
                TempData["SuccessMessage"] = "تنظیمات سئو صفحه حذف گردید.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف سئو صفحه ID {Id}", id);
                TempData["ErrorMessage"] = $"خطا در حذف سئو صفحه: {ex.Message}";
            }
            return RedirectToAction(nameof(PageSeos));
        }

        #endregion
    }
}
