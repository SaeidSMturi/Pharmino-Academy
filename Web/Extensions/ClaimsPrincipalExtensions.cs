using System.Security.Claims;

namespace Web.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// دریافت UserId از Claims
        /// </summary>
        public static int? GetUserId(this ClaimsPrincipal principal)
        {
            var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return null;
        }

        /// <summary>
        /// دریافت UserName از Claims
        /// </summary>
        public static string GetUserName(this ClaimsPrincipal principal)
        {
            return principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? string.Empty;
        }

        /// <summary>
        /// دریافت Email از Claims
        /// </summary>
        public static string GetEmail(this ClaimsPrincipal principal)
        {
            return principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? string.Empty;
        }

        /// <summary>
        /// دریافت FullName از Claims
        /// </summary>
        public static string GetFullName(this ClaimsPrincipal principal)
        {
            return principal.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? string.Empty;
        }

        /// <summary>
        /// بررسی اینکه آیا کاربر وارد شده است
        /// </summary>
        public static bool IsLoggedIn(this ClaimsPrincipal principal)
        {
            return principal?.Identity?.IsAuthenticated ?? false;
        }
    }
}
