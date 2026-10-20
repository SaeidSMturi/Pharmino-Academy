using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace Web.Areas.Admin.Controllers
{
    public class DashboardController : AdminBaseController
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DashboardController> _logger;

        // تزریق ILogger به کانستراکتور
        public DashboardController(IWebHostEnvironment environment, ILogger<DashboardController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var username = User.Identity?.Name ?? "Unknown User";
            _logger.LogInformation("Admin user '{Username}' successfully accessed the dashboard index.", username);

            return View();
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : "Anonymous User";
            var requestPath = HttpContext.Request.Path + HttpContext.Request.QueryString;

            // ثبت هشدار امنیتی برای تلاش‌های ناموفق جهت دسترسی به بخش‌های مختلف
            _logger.LogWarning("Access Denied event triggered. User: '{Username}' attempted to access a protected area. Request Path: {RequestPath}",
                username, requestPath);

            return View();
        }
    }
}