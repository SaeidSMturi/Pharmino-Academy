using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp.Formats.Webp;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Web.Models;
using Application.Interfaces;

namespace Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly WebContext _context;
        private readonly IHlsProcessingService _hlsService;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _memoryCache;

        public HomeController(ILogger<HomeController> logger, WebContext context, IHlsProcessingService hlsService, IWebHostEnvironment env, IMemoryCache memoryCache)
        {
            _logger = logger;
            _context = context;
            _hlsService = hlsService;
            _env = env;
            _memoryCache = memoryCache;
        }

        public async Task<IActionResult> Index()
        {
            var latestCourses = await _context.Courses
                .Where(c => c.IsActive && c.IsListedForSale)
                .OrderByDescending(c => c.CreateDate)
                .Select(c => new HomeCourseDto
                {
                    CourseId = c.CourseId,
                    Title = c.Title,
                    Description = c.Description,
                    Price = c.Price,
                    DurationHours = c.DurationHours ?? 0,
                    ImageName = c.ImageName,
                    VideoCount = c.Videos.Count(v => v.IsActive),
                    IsBundle = c.IsBundle,
                    DiscountPrice = c.DiscountPrice,
                    IsDiscountActive = c.IsDiscountActive,
                    SubCourseCount = c.BundleCourses.Count
                })
                .Take(6)
                .ToListAsync();

            var viewModel = new HomeIndexViewModel
            {
                LatestCourses = latestCourses
            };

            var cultureName = System.Globalization.CultureInfo.CurrentCulture.Name;
            var isFarsi = cultureName.StartsWith("fa", System.StringComparison.OrdinalIgnoreCase);
            var isArabic = cultureName.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);

            if (isFarsi)
            {
                ViewData["SeoTitle"] = "آمادگی آزمون داروسازی عمان (OMSB) و لایسنس داروسازی خلیج فارس | آکادمی فارمینو";
                ViewData["MetaDescription"] = "آمادگی آنلاین آزمون لایسنس داروسازی عمان (OMSB / Pearson VUE) و خلیج فارس: ویدیو، جزوه و آزمون تمرینی از داروسازی که خودش قبول شده است.";
                ViewData["MetaKeywords"] = "آزمون داروسازی عمان, آزمون OMSB, آزمون Pearson VUE داروسازی, لایسنس داروسازی خلیج فارس, کار داروساز در عمان, آمادگی آزمون داروسازی";
            }
            else if (isArabic)
            {
                ViewData["SeoTitle"] = "التحضير لامتحان الصيدلة في عُمان OMSB وترخيص الصيادلة في الخليج | أكاديمية فارمينو";
                ViewData["MetaDescription"] = "تحضير أونلاين لامتحان ترخيص الصيادلة في عُمان (OMSB / Pearson VUE) والخليج: فيديوهات وملفات PDF واختبارات تدريبية من صيدلي اجتاز الامتحان.";
                ViewData["MetaKeywords"] = "امتحان الصيدلة عمان, امتحان OMSB للصيادلة, امتحان بيرسون فيو للصيادلة, ترخيص الصيادلة في الخليج, عمل الصيادلة في عمان, صيادلة مصر, التحضير لامتحان الصيدلة";
            }
            else
            {
                ViewData["SeoTitle"] = "Oman Pharmacist Exam (OMSB) Prep & Gulf Pharmacy Licensing | Pharmino Academy";
                ViewData["MetaDescription"] = "Online prep for the Oman pharmacist exam (OMSB / Pearson VUE) and Gulf licensing: video lessons, PDFs and practice exams from a pharmacist who passed.";
                ViewData["MetaKeywords"] = "Oman pharmacist exam, OMSB pharmacy exam, Pearson VUE pharmacist exam, Oman pharmacy license, Gulf pharmacist licensing exam, pharmacist jobs in Oman, pharmacy exam preparation India, pharmacy exam preparation Pakistan, Egyptian pharmacists Gulf";
            }
            ViewData["OgType"] = "website";

            var baseUrl = Web.Seo.SeoSite.BaseUrl(HttpContext);
            var lang = Web.Seo.SeoSite.Lang(cultureName);
            var itemListTitle = isFarsi ? "جدیدترین دوره‌های آمادگی آزمون داروسازی" : (isArabic ? "أحدث دورات التحضير لامتحان الصيدلة" : "Latest Pharmacy Exam Prep Courses");

            // Organization and WebSite come from _SeoHead on every page; the home page adds the course list.
            var jsonLdGraph = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "ItemList",
                ["name"] = itemListTitle,
                ["itemListElement"] = latestCourses.Select((c, idx) => new Dictionary<string, object>
                {
                    ["@type"] = "ListItem",
                    ["position"] = idx + 1,
                    ["url"] = Web.Seo.SeoSite.LocalizedUrl(baseUrl, $"/Course/{c.CourseId}", lang),
                    ["name"] = c.Title ?? ""
                }).ToArray()
            });

            ViewData["JsonLd"] = jsonLdGraph;

            return View(viewModel);
        }

        [HttpGet("resize")]
        public async Task<IActionResult> ResizeImage(string imagePath, int width, int height, [FromServices] Web.Services.CourseCoverCache covers)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return NotFound();
            width = Math.Clamp(width, 0, 2000);
            height = Math.Clamp(height, 0, 2000);
            if (width == 0 && height == 0) width = 800;

            string cleanImagePath = imagePath.TrimStart('~', '/');
            string cacheKey = $"img_{cleanImagePath}_{width}_{height}";

            if (_memoryCache.TryGetValue(cacheKey, out byte[] cachedImage))
            {
                SetBrowserCacheHeaders();
                return File(cachedImage, "image/webp");
            }

            // Only image files inside wwwroot (the path comes from the query string).
            var webRoot = Path.GetFullPath(_env.WebRootPath);
            var actualPath = Path.GetFullPath(Path.Combine(webRoot, cleanImagePath));
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!actualPath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !imageExtensions.Contains(Path.GetExtension(actualPath).ToLowerInvariant()))
                return NotFound();

            // Course covers: after a redeploy the local copy is gone, so fetch it back from the storage bucket.
            const string coverPrefix = "uploads/courses/";
            byte[]? bucketBytes = null;
            if (!System.IO.File.Exists(actualPath) && cleanImagePath.StartsWith(coverPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var cover = await covers.GetAsync(cleanImagePath.Substring(coverPrefix.Length));
                if (cover?.LocalPath != null) actualPath = cover.LocalPath;
                else bucketBytes = cover?.Bytes;
            }

            if (bucketBytes == null && !System.IO.File.Exists(actualPath))
            {
                // No image anywhere: show the branded default cover (not cached, so a re-uploaded cover appears at once).
                Response.Headers.CacheControl = "no-cache";
                return PhysicalFile(Path.Combine(webRoot, Web.Services.CourseCoverCache.DefaultCoverUrl.TrimStart('/')), "image/svg+xml");
            }

            try
            {
                using (var outputStream = new MemoryStream())
                {
                    // Only JPEG/PNG/GIF/WebP decoders (Web.Services.SafeImageDecoding); first frame only.
                    using (var image = bucketBytes != null
                        ? Image.Load(Web.Services.SafeImageDecoding.Options(maxFrames: 1), bucketBytes)
                        : await Image.LoadAsync(Web.Services.SafeImageDecoding.Options(maxFrames: 1), actualPath))
                    {
                        int targetHeight = height > 0 ? height : (int)(image.Height * ((float)width / image.Width));

                        image.Mutate(x => x
                            .AutoOrient()
                            .Resize(new ResizeOptions
                            {
                                Size = new Size(width, targetHeight),
                                Mode = ResizeMode.Max,
                                Sampler = KnownResamplers.Lanczos3
                            }));

                        image.Metadata.ExifProfile = null;
                        image.Metadata.IccProfile = null;
                        image.Metadata.XmpProfile = null;

                        var encoder = new WebpEncoder
                        {
                            Quality = 75,
                            Method = WebpEncodingMethod.BestQuality,
                            FileFormat = WebpFileFormatType.Lossy
                        };

                        await image.SaveAsWebpAsync(outputStream, encoder);
                    }

                    byte[] imageBytes = outputStream.ToArray();

                    var cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromDays(7))
                        .SetSize(imageBytes.Length);

                    _memoryCache.Set(cacheKey, imageBytes, cacheOptions);

                    SetBrowserCacheHeaders();
                    return File(imageBytes, "image/webp");
                }
            }
            catch (ImageFormatException)
            {
                // Not a JPEG/PNG/GIF/WebP (e.g. a TIFF renamed .png) or a damaged file: show the default cover.
                Response.Headers.CacheControl = "no-cache";
                return PhysicalFile(Path.Combine(webRoot, Web.Services.CourseCoverCache.DefaultCoverUrl.TrimStart('/')), "image/svg+xml");
            }
        }

        private void SetBrowserCacheHeaders()
        {
            Response.Headers.Append("Cache-Control", "public,max-age=2592000");
        }

        // Converts every course video: staff only (was open to anonymous visitors).
        [Web.Attributes.RequireRole]
        [Web.Attributes.Permission("Courses.Edit")]
        [HttpGet("MigrateAllToHls")]
        public async Task<IActionResult> MigrateAllToHls()
        {
            var videos = await _context.CourseVideos.ToListAsync();
            var report = new List<object>();

            foreach (var video in videos)
            {
                var videoPath = Path.Combine(_env.WebRootPath, "uploads", "courses",
                    video.CourseId.ToString(), "videos", video.VideoFileName);

                if (!System.IO.File.Exists(videoPath))
                {
                    report.Add(new { video.VideoId, video.Title, Status = "Skipped - File Not Found" });
                    continue;
                }

                var videoGuid = Path.GetFileNameWithoutExtension(video.VideoFileName);

                if (await _hlsService.HlsExistsAsync(video.CourseId, videoGuid))
                {
                    report.Add(new { video.VideoId, video.Title, Status = "Already Converted" });
                    continue;
                }

                var result = await _hlsService.ProcessVideoAsync(video.CourseId, videoPath, videoGuid);

                if (result.Success)
                {
                    report.Add(new { video.VideoId, video.Title, Status = "Success", Segments = result.SegmentCount });
                }
                else
                {
                    report.Add(new { video.VideoId, video.Title, Status = "Failed", Error = result.Error });
                }
            }

            return Ok(report);
        }

        public IActionResult Privacy()
        {
            var cultureName = System.Globalization.CultureInfo.CurrentCulture.Name;
            var isFarsi = cultureName.StartsWith("fa", System.StringComparison.OrdinalIgnoreCase);
            var isArabic = cultureName.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);

            if (isFarsi)
            {
                ViewData["SeoTitle"] = "سیاست حفظ حریم خصوصی و شرایط استفاده";
                ViewData["MetaDescription"] = "سیاست حفظ حریم خصوصی و شرایط استفاده آکادمی فارمینو: نحوه محافظت از اطلاعات، پرداخت‌ها و دسترسی شما به دوره‌های آنلاین آمادگی آزمون داروسازی.";
                ViewData["MetaKeywords"] = "حریم خصوصی, شرایط استفاده, قوانین پلتفرم, مقررات داروسازی";
            }
            else if (isArabic)
            {
                ViewData["SeoTitle"] = "سياسة الخصوصية وشروط الاستخدام";
                ViewData["MetaDescription"] = "اقرأ سياسة الخصوصية وشروط الاستخدام في أكاديمية فارمينو: كيف نحمي بياناتك ومدفوعاتك ووصولك إلى دورات التحضير لامتحانات الصيدلة أونلاين.";
                ViewData["MetaKeywords"] = "سياسة الخصوصية, شروط الاستخدام, قوانین المنصة, لوائح الصيدليين";
            }
            else
            {
                ViewData["SeoTitle"] = "Privacy Policy & Terms of Use";
                ViewData["MetaDescription"] = "Read the Pharmino Academy privacy policy and terms of use: how we protect your data, payments and access to our online pharmacy exam prep courses.";
                ViewData["MetaKeywords"] = "privacy policy, terms of use, platform rules, pharmacists regulations";
            }
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}