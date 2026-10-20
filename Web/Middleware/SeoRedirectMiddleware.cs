using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Web.Middleware
{
    public class SeoRedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SeoRedirectMiddleware> _logger;

        public SeoRedirectMiddleware(RequestDelegate next, ILogger<SeoRedirectMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISeoService seoService)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";

            // 1. Process Redirects
            // Do not redirect files or assets directly
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
            {
                try
                {
                    var redirect = await seoService.GetRedirectByOldUrlAsync(path);
                    if (redirect != null)
                    {
                        _logger.LogInformation("SEO Redirect triggered: {OldUrl} -> {NewUrl} ({Type})", 
                            path, redirect.NewUrl, redirect.IsPermanent ? "301" : "302");
                        
                        context.Response.Redirect(redirect.NewUrl, redirect.IsPermanent);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking SEO Redirects for path {Path}", path);
                }
            }

            // 2. Call next middleware
            await _next(context);

            // 3. Log 404 & 5xx Errors
            var statusCode = context.Response.StatusCode;
            if (statusCode == 404 || statusCode >= 500)
            {
                if (IsBotOrScannerPath(path, ext))
                {
                    return;
                }

                try
                {
                    var referrerRaw = context.Request.Headers["Referer"].ToString();
                    var referrer = string.IsNullOrWhiteSpace(referrerRaw) ? null : referrerRaw;
                    var userAgentRaw = context.Request.Headers["User-Agent"].ToString();
                    var userAgent = string.IsNullOrWhiteSpace(userAgentRaw) ? null : userAgentRaw;

                    await seoService.LogErrorAsync(path, statusCode, referrer, userAgent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to log SEO Error for path {Path}", path);
                }
            }
        }

        private static bool IsBotOrScannerPath(string path, string extension)
        {
            var ignoredExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".css", ".js", ".map", ".woff", ".woff2", ".ico", ".svg", ".webp" };
            if (ignoredExtensions.Contains(extension))
            {
                return true;
            }

            var botPathTokens = new[]
            {
                "wp-admin", "wp-content", "wp-includes", "wp-json", "wp-login",
                "xmlrpc", ".php", ".env", ".git", "phpmyadmin", "cgi-bin",
                "autodiscover", "setup-config", "actuator", "admin.php", "login.php",
                "install.php", "eval", "shell", "boaform", "v2/", "config.json"
            };

            return botPathTokens.Any(token => path.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class SeoRedirectMiddlewareExtensions
    {
        public static IApplicationBuilder UseSeoRedirectsAndMonitoring(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SeoRedirectMiddleware>();
        }
    }
}
