using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Web.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class PermissionAttribute : TypeFilterAttribute
    {
        public PermissionAttribute(string permission) : base(typeof(PermissionFilter))
        {
            Arguments = new object[] { permission };
        }
    }

    public class PermissionFilter : IAsyncAuthorizationFilter
    {
        private readonly string _permission;
        private readonly IPermissionService _permissionService;

        public PermissionFilter(string permission, IPermissionService permissionService)
        {
            _permission = permission;
            _permissionService = permissionService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            try
            {
                // Check if user is authenticated
                if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
                {
                    SetResult(context, false);
                    return;
                }

                // Get user ID from claims
                var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    SetResult(context, false);
                    return;
                }

                // Check if user has the required permission
                var hasPermission = await _permissionService.UserHasPermissionAsync(userId, _permission);
                if (!hasPermission)
                {
                    SetResult(context, false);
                }
            }
            catch (Exception)
            {
                SetResult(context, false);
            }
        }

        private static void SetResult(AuthorizationFilterContext context, bool _)
        {
            // برای AJAX requests: برگرداندن JSON به جای Redirect
            if (IsAjaxRequest(context.HttpContext))
            {
                context.Result = new JsonResult(new { success = false, message = "Access denied. You don't have permission to perform this action." })
                {
                    StatusCode = 403
                };
            }
            else
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Dashboard", new { area = "Admin" });
            }
        }

        private static bool IsAjaxRequest(HttpContext context)
        {
            return context.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                || context.Request.ContentType?.Contains("application/json") == true
                || context.Request.Headers["Accept"].ToString().Contains("application/json");
        }
    }
}
