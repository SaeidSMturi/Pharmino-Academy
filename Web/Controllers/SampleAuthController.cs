using Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    /// <summary>
    /// نمونه کنترلر برای نمایش استفاده از Authorization
    /// </summary>
    public class SampleAuthController : Controller
    {
        // صفحه عمومی - همه می‌توانند دسترسی داشته باشند
        [AllowAnonymous]
        public IActionResult PublicPage()
        {
            return View();
        }

        // صفحه محافظت شده - فقط کاربران وارد شده
        [Authorize]
        public IActionResult ProtectedPage()
        {
            // دریافت اطلاعات کاربر جاری با استفاده از Extension Methods
            var userId = User.GetUserId();
            var username = User.GetUserName();
            var email = User.GetEmail();
            var fullName = User.GetFullName();

            ViewBag.UserId = userId;
            ViewBag.Username = username;
            ViewBag.Email = email;
            ViewBag.FullName = fullName;

            return View();
        }

        // بررسی وضعیت ورود کاربر
        public IActionResult CheckAuthStatus()
        {
            if (User.IsLoggedIn())
            {
                return Json(new
                {
                    isAuthenticated = true,
                    userId = User.GetUserId(),
                    username = User.GetUserName(),
                    email = User.GetEmail()
                });
            }

            return Json(new
            {
                isAuthenticated = false,
                message = "کاربر وارد نشده است"
            });
        }

        // نمونه Action برای کاربران خاص (با استفاده از Custom Policy)
        // برای استفاده از این، باید در Program.cs Policy تعریف کنید
        /*
        [Authorize(Policy = "RequireAdminRole")]
        public IActionResult AdminOnlyPage()
        {
            return View();
        }
        */

        // نمونه Action با بررسی دستی دسترسی
        [Authorize]
        public IActionResult CustomAuthCheck()
        {
            var userId = User.GetUserId();

            // بررسی دستی شرایط
            if (userId == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            // منطق کاربردی شما
            return View();
        }
    }
}
