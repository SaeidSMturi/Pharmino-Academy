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
                .Where(c => c.IsActive)
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

            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);

            if (isArabic)
            {
                ViewData["Title"] = "الرئيسية | التعليم التخصصي في الصيدلة والعلوم الصيدلانية";
                ViewData["MetaDescription"] = "المنصة الإلكترونية الأكثر شمولاً لتعليم الصيدلة، علم الأدوية، الصيدلة السريرية، وقراءة الوصفات الطبية مع أفضل الدورات والمحاضرين.";
                ViewData["MetaKeywords"] = "تعليم الصيدلة, علم الأدوية, قراءة الوصفات الطبية, الصيدلة السريرية, دورات صيدلة عبر الإنترنت, صيدلي";
            }
            else
            {
                ViewData["Title"] = "Home Page | Specialized Pharmacy & Pharmaceutical Sciences Education";
                ViewData["MetaDescription"] = "The most comprehensive online platform for pharmacy education, pharmacology, clinical pharmacy, and prescription reading featuring top courses and instructors.";
                ViewData["MetaKeywords"] = "pharmacy education, pharmacology, prescription reading, clinical pharmacy, online pharmacy courses, pharmacist";
            }
            ViewData["OgType"] = "website";

            var scheme = Request.Scheme ?? "https";
            var host = Request.Host.Value;

            var siteName = isArabic ? "منصة الصيدليين" : "Pharmacists Platform";
            var siteDesc = isArabic ? "نظام آموزش الصيدلة التخصصي" : "Specialized Pharmacy Education System";
            var itemListTitle = isArabic ? "أحدث دورات الصيدلة" : "Latest Pharmacy Courses";

            var courseListItems = latestCourses.Select((c, idx) => $@"
            {{
                ""@type"": ""ListItem"",
                ""position"": {idx + 1},
                ""item"": {{
                    ""@type"": ""Course"",
                    ""name"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(c.Title ?? "")}"",
                    ""description"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(c.Description ?? "")}"",
                    ""provider"": {{
                        ""@type"": ""Organization"",
                        ""name"": ""{siteName}"",
                        ""sameAs"": ""{scheme}://{host}""
                    }}
                }}
            }}").ToList();

            var jsonLdGraph = $@"
            {{
                ""@context"": ""https://schema.org"",
                ""@graph"": [
                    {{
                        ""@type"": ""WebSite"",
                        ""@id"": ""{scheme}://{host}/#website"",
                        ""url"": ""{scheme}://{host}"",
                        ""name"": ""Pharmacists"",
                        ""description"": ""{siteDesc}""
                    }},
                    {{
                        ""@type"": ""Organization"",
                        ""@id"": ""{scheme}://{host}/#organization"",
                        ""name"": ""{siteName}"",
                        ""url"": ""{scheme}://{host}"",
                        ""logo"": ""{scheme}://{host}/images/logo-og.png""
                    }},
                    {{
                        ""@type"": ""ItemList"",
                        ""name"": ""{itemListTitle}"",
                        ""itemListElement"": [{string.Join(",", courseListItems)}]
                    }}
                ]
            }}";

            ViewData["JsonLd"] = jsonLdGraph;

            return View(viewModel);
        }

        [HttpGet("resize")]
        public async Task<IActionResult> ResizeImage(string imagePath, int width, int height)
        {
            string cleanImagePath = imagePath.TrimStart('~', '/');
            string cacheKey = $"img_{cleanImagePath}_{width}_{height}";

            if (_memoryCache.TryGetValue(cacheKey, out byte[] cachedImage))
            {
                SetBrowserCacheHeaders();
                return File(cachedImage, "image/webp");
            }

            var actualPath = Path.Combine(_env.WebRootPath, cleanImagePath);
            if (!System.IO.File.Exists(actualPath))
            {
                var fallbackSvg = $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'><rect width='100%' height='100%' fill='#123C76'/><text x='50%' y='50%' fill='#E1D788' dominant-baseline='middle' text-anchor='middle' font-family='sans-serif' font-size='16'>Image Not Found</text></svg>";
                return Content(fallbackSvg, "image/svg+xml");
            }

            using (var outputStream = new MemoryStream())
            {
                using (var image = await Image.LoadAsync(actualPath))
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

        private void SetBrowserCacheHeaders()
        {
            Response.Headers.Append("Cache-Control", "public,max-age=2592000");
        }

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
            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
            if (isArabic)
            {
                ViewData["Title"] = "سياسة الخصوصية وشروط الاستخدام";
                ViewData["MetaDescription"] = "بيان سياسة الخصوصية والشروط والأحكام لاستخدام الخدمات التعليمية لمنصة الصيدليين.";
                ViewData["MetaKeywords"] = "سياسة الخصوصية, شروط الاستخدام, قوانین المنصة, لوائح الصيدليين";
            }
            else
            {
                ViewData["Title"] = "Privacy Policy & Terms of Use";
                ViewData["MetaDescription"] = "Privacy policy statement and terms & conditions for using Pharmacists platform educational services.";
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