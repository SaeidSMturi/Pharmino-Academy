using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Web.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ICourseService _courseService;
        private readonly IPayPalService _payPalService;
        private readonly ILogger<CoursesController> _logger;

        public CoursesController(ICourseService courseService, IPayPalService payPalService, ILogger<CoursesController> logger)
        {
            _courseService = courseService;
            _payPalService = payPalService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return 0;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            else
            {
                _logger.LogWarning("Failed to retrieve user ID. Invalid claim value: {UserIdClaim}", userIdClaim);
                return 0;
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Request received to view active courses list.");

                var courses = await _courseService.GetActiveCoursesAsync();

                _logger.LogInformation("Successfully fetched {CourseCount} active courses.", courses?.Count() ?? 0);

                var cultureName = System.Globalization.CultureInfo.CurrentCulture.Name;
                var isFarsi = cultureName.StartsWith("fa", System.StringComparison.OrdinalIgnoreCase);
                var isArabic = cultureName.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);

                if (isFarsi)
                {
                    ViewData["SeoTitle"] = "دوره‌های آمادگی آزمون داروسازی عمان و خلیج فارس";
                    ViewData["MetaDescription"] = "همه دوره‌های آنلاین آمادگی آزمون لایسنس داروسازی عمان (OMSB / Pearson VUE) و کشورهای خلیج فارس: ویدیو، جزوه و آزمون تمرینی. همین حالا ثبت‌نام کنید.";
                    ViewData["MetaKeywords"] = "دوره‌های داروسازی, فارماکولوژی, ثبت‌نام دوره داروسازی, داروسازی بالینی, آزمون داروسازی";
                }
                else if (isArabic)
                {
                    ViewData["SeoTitle"] = "دورات التحضير لامتحان الصيدلة في عُمان والخليج";
                    ViewData["MetaDescription"] = "جميع دورات التحضير لامتحان ترخيص الصيادلة في عُمان (OMSB / Pearson VUE) والخليج: فيديوهات وملفات PDF واختبارات تدريبية. سجّل الآن.";
                    ViewData["MetaKeywords"] = "دورات صيدلة, علم الأدوية, التسجيل في دورات الصيدلة, الصيدلة السريرية, امتحان الصيدلة";
                }
                else
                {
                    ViewData["SeoTitle"] = "Oman & Gulf Pharmacist Exam Prep Courses";
                    ViewData["MetaDescription"] = "All online courses for the Oman pharmacist exam (OMSB / Pearson VUE) and Gulf licensing: video lessons, PDFs and practice exams. Enroll today.";
                    ViewData["MetaKeywords"] = "Oman pharmacist exam course, OMSB exam preparation, Pearson VUE pharmacy exam course, Gulf pharmacy licensing course, online pharmacy courses";
                }
                ViewData["OgType"] = "website";

                var scheme = Request.Scheme ?? "https";
                var host = Request.Host.Value;

                if (courses != null && courses.Any())
                {
                    var courseItems = courses.Select((c, idx) => $@"
                    {{
                        ""@type"": ""ListItem"",
                        ""position"": {idx + 1},
                        ""item"": {{
                            ""@type"": ""Course"",
                            ""name"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(c.Title ?? "")}"",
                            ""description"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(c.Description ?? "")}"",
                            ""url"": ""{Web.Seo.SeoSite.LocalizedUrl(Web.Seo.SeoSite.BaseUrl(HttpContext), $"/Course/{c.CourseId}", Web.Seo.SeoSite.Lang(cultureName))}""
                        }}
                    }}").ToList();

                    var listName = isFarsi ? "فهرست دوره‌های آموزشی داروسازی" : (isArabic ? "قائمة دورات تدريب الصيدلة" : "Pharmacy Training Courses List");

                    ViewData["JsonLd"] = $@"
                    {{
                        ""@context"": ""https://schema.org"",
                        ""@type"": ""ItemList"",
                        ""name"": ""{listName}"",
                        ""itemListElement"": [{string.Join(",", courseItems)}]
                    }}";
                }

                return View(courses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching the courses list in the Index page.");
                TempData["ErrorMessage"] = "An error occurred while fetching courses. Please try again later.";
                return View(new List<Domain.Entities.Course.Course>());
            }
        }

        // "/Course" on its own (no id) used to be a 404; send it to the course list instead.
        [HttpGet("Course")]
        public IActionResult CourseRoot() => RedirectToActionPermanent(nameof(Index));

        [Route("Course/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                _logger.LogInformation("Starting to fetch details for course ID: {CourseId}", id);

                var course = await _courseService.GetCourseWithDetailsAsync(id);

                if (course == null)
                {
                    _logger.LogWarning("Course with ID {CourseId} was not found. User redirected to home page.", id);
                    Response.StatusCode = StatusCodes.Status404NotFound;
                    return View("NotFound");
                }

                var userId = GetCurrentUserId();
                var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                var isEnrolled = false;

                if (isAuthenticated && userId > 0)
                {
                    isEnrolled = await _courseService.IsUserEnrolledAsync(userId, id);
                    _logger.LogInformation("User {UserId} is viewing course {CourseId}. Enrollment status: {IsEnrolled}", userId, id, isEnrolled);
                }
                else
                {
                    _logger.LogInformation("A guest user is viewing course {CourseId}.", id);
                }

                List<Domain.Entities.Course.CourseVideo> videos;
                List<Domain.Entities.Course.CoursePdf> pdfs;
                videos = await _courseService.GetCourseVideosAsync(id, userId);

                if (isEnrolled)
                {
                    pdfs = await _courseService.GetCoursePdfsAsync(id, userId);
                }
                else
                {
                    pdfs = course.Pdfs.Where(p => p.Price == 0).ToList();
                }

                if (course.IsBundle)
                {
                    ViewBag.BundleCourses = await _courseService.GetBundleCoursesAsync(id);
                }

                ViewBag.IsEnrolled = isEnrolled;
                ViewBag.IsAuthenticated = isAuthenticated;
                ViewBag.Videos = videos;
                ViewBag.Pdfs = pdfs;

                var scheme = Request.Scheme ?? "https";
                var host = Request.Host.Value;

                var cultureNameDetails = System.Globalization.CultureInfo.CurrentCulture.Name;
                var isFarsiDetails = cultureNameDetails.StartsWith("fa", System.StringComparison.OrdinalIgnoreCase);
                var isArabicDetails = cultureNameDetails.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
                var rawDescription = System.Text.RegularExpressions.Regex.Replace(course.Description ?? "", "<.*?>", string.Empty);
                // Search engines show 120–160 characters: use the course's own text when it is long enough, else a full sentence about the course.
                rawDescription = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(rawDescription, @"\s+", " ")).Trim();
                var metaDesc = rawDescription.Length >= 120 ? Web.Seo.SeoSite.FitDescription(rawDescription) : "";

                if (isFarsiDetails)
                {
                    ViewData["SeoTitle"] = course.Title;
                    ViewData["MetaDescription"] = string.IsNullOrWhiteSpace(metaDesc) ? Web.Seo.SeoSite.FitDescription($"{course.Title}: دوره آنلاین آکادمی فارمینو برای آمادگی آزمون لایسنس داروسازی عمان (OMSB) و خلیج فارس، همراه با ویدیو، جزوه و سؤالات تمرینی.") : metaDesc;
                    ViewData["MetaKeywords"] = $"{course.Title}, یادگیری {course.Title}, دوره داروسازی, فارماکولوژی";
                }
                else if (isArabicDetails)
                {
                    ViewData["SeoTitle"] = course.Title;
                    ViewData["MetaDescription"] = string.IsNullOrWhiteSpace(metaDesc) ? Web.Seo.SeoSite.FitDescription($"{course.Title}: دورة أونلاين في أكاديمية فارمينو للتحضير لامتحانات ترخيص الصيادلة في عُمان (OMSB) والخليج، مع فيديوهات وملفات PDF وأسئلة تدريبية.") : metaDesc;
                    ViewData["MetaKeywords"] = $"{course.Title}, تعلم {course.Title}, دورة صيدلة, علم الأدوية";
                }
                else
                {
                    ViewData["SeoTitle"] = course.Title;
                    ViewData["MetaDescription"] = string.IsNullOrWhiteSpace(metaDesc) ? Web.Seo.SeoSite.FitDescription($"{course.Title}: online course at Pharmino Academy for the Oman (OMSB) and Gulf pharmacist licensing exams, with video lessons, PDFs and practice questions.") : metaDesc;
                    ViewData["MetaKeywords"] = $"{course.Title}, learn {course.Title}, pharmacy course, pharmacology";
                }
                ViewData["OgType"] = "article";
                var seoBase = Web.Seo.SeoSite.BaseUrl(HttpContext);
                ViewData["CanonicalUrl"] = Web.Seo.SeoSite.LocalizedUrl(seoBase, $"/Course/{course.CourseId}", Web.Seo.SeoSite.CurrentLang);

                if (!string.IsNullOrEmpty(course.ImageName) && course.ImageName != "default.jpg")
                {
                    ViewData["OgImage"] = $"{seoBase}/uploads/courses/{course.ImageName}";
                }

                var providerName = Web.Seo.SeoSite.BrandName(cultureNameDetails);

                var courseJsonLd = $@"
                {{
                    ""@context"": ""https://schema.org"",
                    ""@type"": ""Course"",
                    ""name"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(course.Title ?? "")}"",
                    ""description"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(metaDesc)}"",
                    ""provider"": {{
                        ""@type"": ""Organization"",
                        ""name"": ""{providerName}"",
                        ""url"": ""{seoBase}/""
                    }},
                    ""offers"": {{
                        ""@type"": ""Offer"",
                        ""price"": ""{course.EffectivePrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}"",
                        ""priceCurrency"": ""{Application.DTOs.Payment.CurrencyHelper.SiteCurrency}"",
                        ""availability"": ""https://schema.org/InStock""
                    }},
                    ""hasCourseInstance"": {{
                        ""@type"": ""CourseInstance"",
                        ""courseMode"": ""Online"",
                        ""instructor"": {{
                            ""@type"": ""Organization"",
                            ""name"": ""{providerName}""
                        }}
                    }}
                }}";

                ViewData["JsonLd"] = courseJsonLd;

                return View(course);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching details, videos, or PDFs for course {CourseId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading course details.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enroll(int courseId)
        {
            try
            {
                if (!User.Identity?.IsAuthenticated ?? true)
                {
                    _logger.LogWarning("Unauthorized user attempt to enroll in course {CourseId}", courseId);
                    return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", new { id = courseId }) });
                }

                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    _logger.LogWarning("Enrollment in course {CourseId} failed because the logged-in user ID was resolved to 0.", courseId);
                    TempData["ErrorMessage"] = "User not found";
                    return RedirectToAction(nameof(Details), new { id = courseId });
                }

                _logger.LogInformation("Enrollment request received from user {UserId} for course {CourseId}.", userId, courseId);

                var course = await _courseService.GetCourseByIdAsync(courseId);
                if (course == null)
                {
                    _logger.LogWarning("User {UserId} requested enrollment for course {CourseId}, which does not exist in the database.", userId, courseId);
                    TempData["ErrorMessage"] = "Course not found";
                    return RedirectToAction(nameof(Index));
                }

                if (!course.IsListedForSale)
                {
                    _logger.LogWarning("User {UserId} attempted to enroll in unlisted course {CourseId}.", userId, courseId);
                    TempData["ErrorMessage"] = "This course is not available for standalone purchase. Please enroll via its bundle package.";
                    return RedirectToAction(nameof(Details), new { id = courseId });
                }

                var isEnrolled = await _courseService.IsUserEnrolledAsync(userId, courseId);
                if (isEnrolled)
                {
                    _logger.LogInformation("User {UserId} was already enrolled in course {CourseId} and attempted to enroll again.", userId, courseId);
                    TempData["InfoMessage"] = "You are already enrolled in this course";
                    return RedirectToAction(nameof(Details), new { id = courseId });
                }

                if (course.EffectivePrice == 0)
                {
                    var enrolled = await _courseService.EnrollUserInCourseAsync(userId, courseId, 0);
                    if (enrolled)
                    {
                        _logger.LogInformation("Enrollment successful (Free): User {UserId} enrolled in course {CourseId}.", userId, courseId);
                        TempData["SuccessMessage"] = "Your enrollment in this free course was successfully completed.";
                    }
                    else
                    {
                        _logger.LogWarning("Enrollment operation for user {UserId} in course {CourseId} failed.", userId, courseId);
                        TempData["ErrorMessage"] = "Error occurred during course enrollment.";
                    }
                    return RedirectToAction(nameof(Details), new { id = courseId });
                }

                // Paid course: Redirect to Checkout page
                return RedirectToAction("Checkout", "Payment", new { courseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during enrollment of user {UserId} in course {CourseId}", GetCurrentUserId(), courseId);
                TempData["ErrorMessage"] = "A system error occurred during enrollment.";
                return RedirectToAction(nameof(Details), new { id = courseId });
            }
        }
    }
}