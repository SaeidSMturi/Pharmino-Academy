using Domain.Configuration;
using Application.DTOs.Account;
using Web.Extensions;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using System.Security.Claims;
using Web.Models;
using Application.DTOs.User;
using Infrastructure.Services;

using Domain.Interfaces;
namespace Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IUserRepository _userRepository;
        private readonly IPermissionService _permissionService;
        private readonly ISessionManagerService _sessionManagerService;
        private readonly IDeviceFingerprintService _deviceFingerprintService;
        private readonly IImpossibleTravelDetectionService _impossibleTravelDetectionService;
        private readonly SessionSecurityOptions _sessionSecurityOptions;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAccountService accountService,
            IUserRepository userRepository,
            IPermissionService permissionService,
            ISessionManagerService sessionManagerService,
            IDeviceFingerprintService deviceFingerprintService,
            IImpossibleTravelDetectionService impossibleTravelDetectionService,
            IOptions<SessionSecurityOptions> sessionSecurityOptions,
            ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _userRepository = userRepository;
            _permissionService = permissionService;
            _sessionManagerService = sessionManagerService;
            _deviceFingerprintService = deviceFingerprintService;
            _impossibleTravelDetectionService = impossibleTravelDetectionService;
            _sessionSecurityOptions = sessionSecurityOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// ثبت یک نشست جدید در SQL Server برای کاربر تازه احراز هویت‌شده و اجرای هیوریستیک تشخیص سفر غیرممکن
        /// Registers a brand-new SQL Server-backed session for a just-authenticated user (Feature 1), applying the
        /// configured concurrent-login limit, and then runs the Impossible-Travel heuristic (Feature 3) against
        /// the user's last known login location. Returns the generated SessionId to embed in the auth cookie's
        /// claims, along with the device fingerprint that the cookie will be bound to (Feature 2).
        /// </summary>
        private async Task<(string SessionId, string DeviceFingerprint)> EstablishSessionAsync(int userId)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
            var deviceFingerprint = _deviceFingerprintService.GenerateFingerprint(userAgent, ipAddress);
            var sessionId = Guid.NewGuid().ToString();
            var nowUtc = DateTime.UtcNow;

            var session = new SessionRecord
            {
                SessionId = sessionId,
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceFingerprint = deviceFingerprint,
                CreatedAtUtc = nowUtc,
                LastActivityUtc = nowUtc,
                ExpiresAtUtc = nowUtc.AddHours(_sessionSecurityOptions.SessionExpirationHours)
            };

            var evictedSessionIds = await _sessionManagerService.CreateSessionAsync(session, _sessionSecurityOptions.MaxConcurrentLogins);
            if (evictedSessionIds.Count > 0)
            {
                _logger.LogInformation(
                    "UserId {UserId} exceeded MaxConcurrentLogins ({Max}); evicted {Count} oldest session(s).",
                    userId, _sessionSecurityOptions.MaxConcurrentLogins, evictedSessionIds.Count);
            }

            var travelResult = await _impossibleTravelDetectionService.EvaluateLoginAsync(userId, sessionId, ipAddress);
            if (travelResult.IsSuspicious)
            {
                _logger.LogCritical(
                    "🚨 UserId {UserId} login flagged as Impossible Travel from IP {Ip}; {Revoked} older session(s) force-revoked.",
                    userId, ipAddress, travelResult.RevokedSessionCount);
            }

            return (sessionId, deviceFingerprint);
        }

        #region Register

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard", new { area = "User" });
            }
            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
            if (isArabic)
            {
                ViewData["Title"] = "إنشاء حساب جدید";
                ViewData["MetaDescription"] = "إنشاء حساب جديد في المنصة التخصصية لتعليم الصيدلة.";
            }
            else
            {
                ViewData["Title"] = "Register Account";
                ViewData["MetaDescription"] = "Create a new account on the specialized pharmacy education platform.";
            }
            ViewData["MetaRobots"] = "noindex, follow";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accountService.RegisterAsync(model);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message + ". Please login.";
            return RedirectToAction("Login");
        }

        #endregion

        #region Login

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity!.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard", new { area = "User" });
            }

            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
            if (isArabic)
            {
                ViewData["Title"] = "تسجيل الدخول إلى الحساب";
                ViewData["MetaDescription"] = "تسجيل دخول المستخدمين والصيادلة للوصول إلى الدورات التعليمية والحساب الشخصي.";
            }
            else
            {
                ViewData["Title"] = "Login to Account";
                ViewData["MetaDescription"] = "User and pharmacist login to access educational courses and personal account.";
            }
            ViewData["MetaRobots"] = "noindex, follow";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid) return View(model);

            var result = await _accountService.LoginAsync(model);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message);
                return View(model);
            }

            var user = await _userRepository.GetUserByIdAsync(result.UserId!.Value);

            // ثبت نشست جدید در SQL Server (اجرای محدودیت ورود همزمان + بررسی سفر غیرممکن)
            // Register the new session in SQL Server (enforces concurrent-login limit + impossible-travel heuristic).
            var (sessionId, _) = await EstablishSessionAsync(user.UserId);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Email ?? user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("FullName", $"{user.FirstName} {user.LastName}".Trim()),
                new Claim("FirstName", $"{user.FirstName}".Trim()),
                new Claim("SessionId", sessionId), // Bound to the SQL Server-backed session record created above
                new Claim("PreferredLanguage", user.PreferredLanguage ?? string.Empty)
            };

            model.RememberMe = true;
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // ... ادامه کدهای ریدایرکت شما ...
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            var isAdmin = await _permissionService.UserHasAnyRoleAsync(user.UserId);
            return RedirectToAction("Index", "Dashboard", new { area = isAdmin ? "Admin" : "User" });
        }
        #endregion


        #region Forgot Password

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
            if (isArabic)
            {
                ViewData["Title"] = "نسيان كلمة المرور";
                ViewData["MetaDescription"] = "استعادة كلمة مرور الحساب في منصة الصيدليين.";
            }
            else
            {
                ViewData["Title"] = "Forgot Password";
                ViewData["MetaDescription"] = "Recover your account password on the Pharmacists platform.";
            }
            ViewData["MetaRobots"] = "noindex, follow";
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Build the reset link from the configured site URL, not from the request's Host header,
            // so a forged Host header can't make the email point at another domain.
            var configuredBase = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Smtp:SiteBaseUrl"];
            var baseUrl = string.IsNullOrWhiteSpace(configuredBase)
                ? "https://pharminoacademy.com"
                : configuredBase.TrimEnd('/');
            var resetPageUrl = baseUrl + Url.Action("ResetPassword", "Account");

            var result = await _accountService.ForgotPasswordAsync(model, resetPageUrl);
            TempData["SuccessMessage"] = result.Message;

            return RedirectToAction("Login");
        }

        #endregion

        #region Reset Password

        [HttpGet]
        public IActionResult ResetPassword(string email, string code)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                return RedirectToAction("Login");
            }

            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
            ViewData["Title"] = isArabic ? "إعادة ضبط كلمة المرور" : "Reset Password";
            ViewData["MetaRobots"] = "noindex, follow";
            var model = new ResetPasswordDto
            {
                Email = email,
                Code = code
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _accountService.ResetPasswordAsync(model);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Login");
        }

        #endregion

        #region Logout

        public async Task<IActionResult> Logout()
        {
            // ابطال نشست SQL Server کاربر جاری تا بلافاصله از داشبورد «نشست‌های فعال» ناپدید شود
            // Revoke the current user's SQL Server session so it disappears from the Active Sessions dashboard
            // immediately instead of lingering until the next periodic cleanup sweep.
            var userId = User.GetUserId();
            var sessionId = User.FindFirst("SessionId")?.Value;
            if (userId.HasValue && !string.IsNullOrEmpty(sessionId))
            {
                await _sessionManagerService.RevokeSessionAsync(userId.Value, sessionId);
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        #endregion

        #region Account Locked

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> AccountLocked()
        {
            // Get current user ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return NotFound();
            }

            // Check if user is actually locked
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null || !user.IsLockedForSuspiciousActivity)
            {
                return NotFound();
            }

            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
            ViewData["Title"] = isArabic ? "الحساب مغلق" : "Account Locked";
            ViewData["MetaRobots"] = "noindex, nofollow";
            return View();
        }

        #endregion

        #region Access Denied

        [HttpGet]
        public IActionResult AccessDenied()
        {
            var isArabic = System.Globalization.CultureInfo.CurrentCulture.Name.StartsWith("ar", System.StringComparison.OrdinalIgnoreCase);
            ViewData["Title"] = isArabic ? "تم رفض الوصول" : "Access Denied";
            ViewData["MetaRobots"] = "noindex, nofollow";
            return View();
        }

        #endregion
    }
}
