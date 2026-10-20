using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Web.Attributes
{
    /// <summary>
    /// Authorization attribute that requires the user to have at least one role assigned.
    /// Returns 404 Not Found if user has no roles.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class RequireRoleAttribute : TypeFilterAttribute
    {
        public RequireRoleAttribute() : base(typeof(RequireRoleFilter))
        {
        }
    }

    public class RequireRoleFilter : IAsyncAuthorizationFilter
    {
        private readonly IPermissionService _permissionService;

        public RequireRoleFilter(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "", returnUrl = context.HttpContext.Request.Path });
                return;
            }

            // Get user ID from claims
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                // User has no valid ID claim - return 404
                context.Result = new NotFoundResult();
                return;
            }

            // Check if user has any roles
            var hasAnyRole = await _permissionService.UserHasAnyRoleAsync(userId);

            if (!hasAnyRole)
            {
                // User has no roles - return 404 to hide admin area existence
                context.Result = new NotFoundResult();
                return;
            }
        }
    }
}
