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

                var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);

                if (isArabic)
                {
                    ViewData["Title"] = "دورات الصيدلة التخصصية | قائمة جميع الدورات";
                    ViewData["MetaDescription"] = "استكشف وسجل في أحدث دورات الصيدلة التخصصية، علم الأدوية، الصيدلة السريرية، والإعداد لاختبارات البورد.";
                    ViewData["MetaKeywords"] = "دورات صيدلة, علم الأدوية, التسجيل في دورات الصيدلة, الصيدلة السريرية, امتحان الصيدلة";
                }
                else
                {
                    ViewData["Title"] = "Specialized Pharmacy Courses | All Courses List";
                    ViewData["MetaDescription"] = "Explore and enroll in the latest specialized pharmacy, pharmacology, clinical pharmacy, and board exam preparation courses.";
                    ViewData["MetaKeywords"] = "pharmacy courses, pharmacology, pharmacy course enrollment, clinical pharmacy, pharmacy exam";
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
                            ""url"": ""{scheme}://{host}/Course/{c.CourseId}""
                        }}
                    }}").ToList();

                    var listName = isArabic ? "قائمة دورات تدريب الصيدلة" : "Pharmacy Training Courses List";

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
                    TempData["ErrorMessage"] = "Course not found";
                    return RedirectToAction(nameof(Index));
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

                var isArabicDetails = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
                var rawDescription = System.Text.RegularExpressions.Regex.Replace(course.Description ?? "", "<.*?>", string.Empty);
                var metaDesc = rawDescription.Length > 160 ? rawDescription.Substring(0, 157) + "..." : rawDescription;

                if (isArabicDetails)
                {
                    ViewData["Title"] = $"{course.Title} | دورة صيدلة تخصصية";
                    ViewData["MetaDescription"] = string.IsNullOrWhiteSpace(metaDesc) ? $"دورة تدريبية تخصصية لـ {course.Title} على منصة الصيدليين." : metaDesc;
                    ViewData["MetaKeywords"] = $"{course.Title}, تعلم {course.Title}, دورة صيدلة, علم الأدوية";
                }
                else
                {
                    ViewData["Title"] = $"{course.Title} | Specialized Pharmacy Course";
                    ViewData["MetaDescription"] = string.IsNullOrWhiteSpace(metaDesc) ? $"Specialized training course for {course.Title} on the Pharmacists platform." : metaDesc;
                    ViewData["MetaKeywords"] = $"{course.Title}, learn {course.Title}, pharmacy course, pharmacology";
                }
                ViewData["OgType"] = "article";
                ViewData["CanonicalUrl"] = $"{scheme}://{host}/Course/{course.CourseId}";

                if (!string.IsNullOrEmpty(course.ImageName) && course.ImageName != "default.jpg")
                {
                    ViewData["OgImage"] = $"{scheme}://{host}/uploads/courses/{course.ImageName}";
                }

                var providerName = isArabicDetails ? "منصة الصيدليين" : "Pharmacists Platform";

                var courseJsonLd = $@"
                {{
                    ""@context"": ""https://schema.org"",
                    ""@type"": ""Course"",
                    ""name"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(course.Title ?? "")}"",
                    ""description"": ""{System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(metaDesc)}"",
                    ""provider"": {{
                        ""@type"": ""Organization"",
                        ""name"": ""{providerName}"",
                        ""sameAs"": ""{scheme}://{host}""
                    }},
                    ""offers"": {{
                        ""@type"": ""Offer"",
                        ""price"": ""{course.Price}"",
                        ""priceCurrency"": ""USD"",
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