using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class SecurityController : Controller
    {
        private readonly ISuspiciousActivityService _suspiciousActivityService;
        private readonly ILogger<SecurityController> _logger;

        // حد آستانه برای مسدودسازی که در سرویس شما 5 تعریف شده بود
        private const int BAN_THRESHOLD = 5;

        public SecurityController(
            ISuspiciousActivityService suspiciousActivityService,
            ILogger<SecurityController> logger)
        {
            _suspiciousActivityService = suspiciousActivityService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> LockedAccounts()
        {
            var lockedUsers = await _suspiciousActivityService.GetLockedUsersAsync();
            return View(lockedUsers);
        }

        [HttpGet]
        public async Task<IActionResult> UserDetails(int userId)
        {
            var currentAdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Admin {AdminId} is viewing suspicious activities for Target User: {TargetUserId}.", currentAdminId, userId);

            // واکشی تمامی رکوردها (حتی کوچکترین هشدارها)
            var activities = await _suspiciousActivityService.GetUserSuspiciousActivitiesAsync(userId, 1, 100);
            var lockHistory = await _suspiciousActivityService.GetAccountLockHistoryAsync(userId);

            // تحلیل وضعیت دقیق مسدودی کاربر
            var isLocked = await _suspiciousActivityService.IsAccountLockedAsync(userId);
            var recentSuspiciousCount = await _suspiciousActivityService.GetSuspiciousActivityCountAsync(userId, 30);

            ViewBag.Activities = activities;
            ViewBag.LockHistory = lockHistory;
            ViewBag.UserId = userId;

            // ارسال اطلاعات تحلیلی برای نمایش دقیق دلایل در View
            ViewBag.IsLocked = isLocked;
            ViewBag.RecentSuspiciousCount = recentSuspiciousCount;
            ViewBag.BanThreshold = BAN_THRESHOLD;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockAccount(int userId, string adminNotes)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (string.IsNullOrWhiteSpace(adminNotes))
            {
                TempData["ErrorMessage"] = "لطفاً یادداشت ادمین را وارد کنید.";
                return RedirectToAction("UserDetails", new { userId });
            }

            var result = await _suspiciousActivityService.UnlockAccountAsync(userId, adminId, adminNotes);

            if (result)
                TempData["SuccessMessage"] = "اکانت کاربر با موفقیت باز شد و تمامی امتیازهای منفی ریست شدند.";
            else
                TempData["ErrorMessage"] = "خطا در باز کردن اکانت.";

            return RedirectToAction("UserDetails", new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewActivity(int activityId, string adminNotes)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _suspiciousActivityService.ReviewActivityAsync(activityId, adminId, adminNotes);

            if (result) return Json(new { success = true, message = "رفتار مشکوک با موفقیت بررسی و ثبت شد." });

            return Json(new { success = false, message = "خطا در بررسی رفتار مشکوک." });
        }

        // تکمیل اکشن برای نمایش لاگ‌های کل سیستم
        [HttpGet]
        public async Task<IActionResult> AllSuspiciousActivities(int page = 1)
        {
            var currentAdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Admin {AdminId} accessed the Global Suspicious Activities log.", currentAdminId);

            var allActivities = await _suspiciousActivityService.GetAllSystemSuspiciousActivitiesAsync(page, 100);
            return View(allActivities);
        }
    }
}