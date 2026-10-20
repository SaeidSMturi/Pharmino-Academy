using Application.Interfaces;
using System.Security.Claims;

namespace Web.Middleware
{
    public class AccountLockMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AccountLockMiddleware> _logger;

        public AccountLockMiddleware(RequestDelegate next, ILogger<AccountLockMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISuspiciousActivityService suspiciousActivityService)
        {
            // ✅ استثنا کردن صفحه AccountLocked و Logout برای جلوگیری از redirect loop
            var path = context.Request.Path.Value?.ToLower() ?? "";
            if (path.Contains("/account/accountlocked") ||
                path.Contains("/account/logout") ||
                path.Contains("/account/login"))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    var isLocked = await suspiciousActivityService.IsAccountLockedAsync(userId);

                    if (isLocked)
                    {
                        _logger.LogWarning("🔒 Locked account attempted access - UserId: {UserId}, Path: {Path}",
                            userId, context.Request.Path);

                        // ریدایرکت به صفحه AccountLocked
                        context.Response.Redirect("/Account/AccountLocked");
                        return;
                    }
                }
            }

            await _next(context);
        }
    }

    public static class AccountLockMiddlewareExtensions
    {
        public static IApplicationBuilder UseAccountLockCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AccountLockMiddleware>();
        }
    }
}
