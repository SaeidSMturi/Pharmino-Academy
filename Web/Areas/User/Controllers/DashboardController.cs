using Application.DTOs.User;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

using Domain.Interfaces;
namespace Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly ICourseService _courseService; // سرویس دوره‌ها اضافه شد

        public DashboardController(IUserRepository userRepository, ICourseService courseService)
        {
            _userRepository = userRepository;
            _courseService = courseService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user == null)
            {
                return RedirectToAction("Logout", "Account", new { area = "" });
            }

            ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
            ViewBag.Email = user.Email;
            ViewBag.RegisterDate = user.RegisterDate.ToString("MMMM dd, yyyy");

            // 1. دریافت لیست تمام ثبت‌نام‌های کاربر
            // اگر متد دریافت ثبت‌نام‌ها نام دیگری دارد، آن را جایگزین کنید
            var enrollments = await _courseService.GetUserEnrollmentsAsync(userId) ?? new List<Domain.Entities.Course.CourseEnrollment>();

            // 2. محاسبه آمار به صورت داینامیک
            var model = new UserDashboardViewModel
            {
                TotalCourses = enrollments.Count(),
                CompletedCourses = enrollments.Count(e => e.IsCompleted),
                ActiveCourses = enrollments.Count(e => !e.IsCompleted),
                // میانگین درصد پیشرفت دوره‌ها
                AverageProgress = enrollments.Any() ? enrollments.Average(e => e.ProgressPercentage) : 0,
                // گرفتن ۴ دوره آخری که کاربر ثبت‌نام کرده
                RecentEnrollments = enrollments.OrderByDescending(e => e.EnrollmentDate).Take(4).ToList()
            };

            // پاس دادن اطلاعات داینامیک به ویو
            return View(model);
        }
    }
}
