using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Infrastructure.Persistence;
using Web.Extensions;

namespace Web.Controllers
{
    public class LanguageController : Controller
    {
        private readonly WebContext _context;

        public LanguageController(WebContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> SetLanguage(string culture, string returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl))
            {
                returnUrl = "/";
            }

            // Safety check for Open Redirect Vulnerability
            if (!Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions 
                { 
                    Expires = DateTimeOffset.UtcNow.AddYears(1), 
                    HttpOnly = true, 
                    Secure = true, 
                    SameSite = SameSiteMode.Lax 
                }
            );

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.GetUserId();
                if (userId.HasValue)
                {
                    var user = await _context.Users.FindAsync(userId.Value);
                    if (user != null && user.PreferredLanguage != culture)
                    {
                        user.PreferredLanguage = culture;
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return LocalRedirect(returnUrl);
        }
    }
}
