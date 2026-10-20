using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Web.Models;
using Application.Interfaces;

namespace Web.Areas.User.Controllers
{
    /// <summary>
    /// کنترلر داشبورد «نشست‌های فعال» - نمایش دستگاه‌های متصل کاربر و امکان ابطال از راه دور هر نشست
    /// User-facing "Active Sessions" dashboard (Feature 4). Lets the signed-in user review every device
    /// currently authenticated to their account (parsed OS/Browser, IP, login/last-activity time) and
    /// remotely terminate (revoke) any session other than the one they're currently browsing with.
    /// </summary>
    [Area("User")]
    [Authorize]
    public class SessionsController : Controller
    {
        private readonly ISessionManagerService _sessionManagerService;
        private readonly IUserAgentParserService _userAgentParserService;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            ISessionManagerService sessionManagerService,
            IUserAgentParserService userAgentParserService,
            ILogger<SessionsController> logger)
        {
            _sessionManagerService = sessionManagerService;
            _userAgentParserService = userAgentParserService;
            _logger = logger;
        }

        /// <summary>
        /// نمایش فهرست تمام نشست‌های فعال کاربر جاری، مرتب‌شده بر اساس آخرین فعالیت (جدیدترین ابتدا)
        /// Displays every active session belonging to the current user, most-recently-active first, with
        /// the caller's own current session clearly flagged so it can be visually distinguished (and
        /// excluded from the "Revoke" action) in the view.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var currentSessionId = User.FindFirst("SessionId")?.Value;

            var sessions = await _sessionManagerService.GetActiveSessionsAsync(userId);

            var viewModels = sessions.Select(s =>
            {
                var (os, browser) = _userAgentParserService.Parse(s.UserAgent);
                return new ActiveSessionViewModel
                {
                    SessionId = s.SessionId,
                    IpAddress = s.IpAddress,
                    OperatingSystem = os,
                    Browser = browser,
                    CreatedAtUtc = s.CreatedAtUtc,
                    LastActivityUtc = s.LastActivityUtc,
                    ExpiresAtUtc = s.ExpiresAtUtc,
                    IsCurrentSession = s.SessionId == currentSessionId
                };
            })
            .OrderByDescending(s => s.IsCurrentSession)
            .ThenByDescending(s => s.LastActivityUtc)
            .ToList();

            return View(viewModels);
        }

        /// <summary>
        /// ابطال یک نشست مشخص با تأیید مالکیت آن نشست توسط کاربر جاری (جلوگیری از ابطال نشست سایر کاربران)
        /// Revokes a single session by id. Ownership is re-validated server-side (the target session's
        /// UserId must match the caller) before revocation so one user can never terminate another user's
        /// session by guessing/tampering with a SessionId value.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(string sessionId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var currentSessionId = User.FindFirst("SessionId")?.Value;

            if (string.IsNullOrEmpty(sessionId))
            {
                return BadRequest();
            }

            var targetSession = await _sessionManagerService.GetSessionAsync(sessionId);
            if (targetSession == null || targetSession.UserId != userId)
            {
                _logger.LogWarning(
                    "UserId {UserId} attempted to revoke SessionId {SessionId} that does not belong to them.",
                    userId, sessionId);
                return Forbid();
            }

            await _sessionManagerService.RevokeSessionAsync(userId, sessionId);

            if (sessionId == currentSessionId)
            {
                // کاربر نشست فعلی خودش را ابطال کرده است؛ باید بلافاصله از سیستم خارج شود
                // The user revoked their own current session; they must be signed out immediately.
                return RedirectToAction("Logout", "Account", new { area = "" });
            }

            TempData["SuccessMessage"] = "Session terminated successfully.";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// ابطال تمام نشست‌های دیگر کاربر (به جز نشست فعلی) - برای پاک‌سازی سریع تمام دستگاه‌های دیگر
        /// Revokes every other active session for the current user in one action, keeping only the
        /// session the caller is currently browsing with — a quick "sign out everywhere else" control.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAllOthers()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var currentSessionId = User.FindFirst("SessionId")?.Value;

            var revokedCount = await _sessionManagerService.RevokeAllSessionsAsync(userId, exceptSessionId: currentSessionId);

            TempData["SuccessMessage"] = revokedCount > 0
                ? $"Signed out of {revokedCount} other device(s)."
                : "No other active sessions were found.";

            return RedirectToAction("Index");
        }
    }
}
