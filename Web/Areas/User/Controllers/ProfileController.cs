using Application.DTOs.User;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

using Domain.Interfaces;
namespace Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUserService _userService;
        private readonly IUserRepository _userRepository;

        public ProfileController(IUserService userService, IUserRepository userRepository)
        {
            _userService = userService;
            _userRepository = userRepository;
        }

        #region Edit Profile

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var profile = await _userService.GetUserProfileAsync(userId);

            if (profile == null)
            {
                return RedirectToAction("Logout", "Account", new { area = "" });
            }

            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _userService.EditProfileAsync(userId, model);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message);
                return View(model);
            }

            // Update claims
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user != null)
            {
                // حفظ Claim مربوط به SessionId هنگام صدور مجدد کوکی - در غیر این صورت SessionValidationCookieEvents
                // این کاربر را در همان درخواست بعدی به دلیل نبود SessionId خارج می‌کند
                // Preserve the existing "SessionId" claim when re-issuing the cookie - otherwise
                // SessionValidationCookieEvents would reject this principal (missing SessionId) on the very
                // next request, effectively logging the user out immediately after a profile edit.
                var existingSessionId = User.FindFirst("SessionId")?.Value;

                var claims = new List<Claim>
     {
      new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
      new Claim(ClaimTypes.Name, user.UserName),
      new Claim(ClaimTypes.Email, user.Email ?? ""),
           new Claim("FullName", $"{user.FirstName} {user.LastName}".Trim()),
         new Claim("FirstName", $"{user.FirstName}".Trim())
        };

                if (!string.IsNullOrEmpty(existingSessionId))
                {
                    claims.Add(new Claim("SessionId", existingSessionId));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                };

                await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                 new ClaimsPrincipal(claimsIdentity),
                   authProperties);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("EditProfile");
        }

        #endregion

        #region Change Password

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _userService.ChangePasswordAsync(userId, model);

            if (!result.Success)
            {
                ModelState.AddModelError("", result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("ChangePassword");
        }

        #endregion
    }
}
