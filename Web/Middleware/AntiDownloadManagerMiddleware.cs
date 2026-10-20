using System.Text.RegularExpressions;
using Application.Interfaces;

namespace Web.Middleware
{
    /// <summary>
    /// Middleware فوق پیشرفته برای تشخیص و جلوگیری از دانلود ویدیو
    /// نسخه بهینه‌شده با قابلیت Real-time Monitoring و رفع باگ‌های مسدودسازی
    /// </summary>
    public class AntiDownloadManagerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AntiDownloadManagerMiddleware> _logger;

        // استفاده از Regex کامپایل‌شده برای پرفورمنس بسیار بالاتر در مقایسه با آرایه و حلقه
        private static readonly Regex DownloadManagerRegex = new Regex(
            @"(idm|internet download manager|wget|curl|aria2|aria2c|fdm|free download manager|getright|jdownloader|flashget|orbit downloader|download accelerator|eagleget|download master|mass downloader|ninja download)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public AntiDownloadManagerMiddleware(RequestDelegate next, ILogger<AntiDownloadManagerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IVideoSessionManager sessionManager)
        {
            // بررسی مسیرهای محافظت‌شده: هم مسیر MP4 قدیمی و هم مسیرهای HLS جدید
            var path = context.Request.Path;
            var isProtectedPath =
                path.StartsWithSegments("/User/Courses/StreamVideo", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/User/Hls/Manifest", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/User/Hls/Segment", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/User/Hls/Key", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWithSegments("/User/Courses/PdfPage", StringComparison.OrdinalIgnoreCase);

            if (isProtectedPath)
            {
                var userAgent = context.Request.Headers["User-Agent"].ToString();
                var referer = context.Request.Headers["Referer"].ToString();
                var accept = context.Request.Headers["Accept"].ToString();
                var acceptLanguage = context.Request.Headers["Accept-Language"].ToString();
                var ipAddress = context.Connection.RemoteIpAddress?.ToString();

                var suspicionScore = 0;
                var reasons = new List<string>();

                // 1. بررسی User-Agent (مهمترین - بهینه‌شده با Regex)
                if (!string.IsNullOrEmpty(userAgent) && DownloadManagerRegex.IsMatch(userAgent))
                {
                    suspicionScore += 100;
                    reasons.Add("DownloadManager-UA");

                    _logger.LogError(
                        "🚫 BLOCKED: Download manager in User-Agent - IP: {IP}, UA: {UserAgent}",
                        ipAddress, userAgent
                    );

                    // فایل جعلی (بهینه‌شده با Random.Shared در نسخه‌های جدید دات‌نت)
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "video/mp4";
                    var fakeData = new byte[1024];
                    Random.Shared.NextBytes(fakeData);
                    await context.Response.Body.WriteAsync(fakeData);
                    return;
                }

                // 2. بررسی Session-based (برای MP4) یا sid param (برای HLS)
                var sessionId = context.Request.Query["sid"].ToString();
                if (string.IsNullOrEmpty(sessionId))
                    sessionId = context.Request.Query["sid"].ToString();

                if (!string.IsNullOrEmpty(sessionId))
                {
                    var session = sessionManager.GetSession(sessionId);

                    if (session != null)
                    {
                        if (sessionManager.ShouldBlockStream(sessionId))
                        {
                            _logger.LogError(
                                "🚫 STREAM BLOCKED by behavior analysis - SessionId: {SessionId}, " +
                                "Score: {Score}, Reasons: {Reasons}",
                                sessionId, session.SuspicionScore,
                                string.Join(", ", session.SuspicionReasons.Keys)
                            );

                            context.Response.StatusCode = 403;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                error = "Stream blocked due to suspicious activity",
                                code = "DOWNLOAD_DETECTED",
                                suspicionScore = session.SuspicionScore
                            });
                            return;
                        }

                        if (sessionManager.ShouldThrottleStream(sessionId))
                        {
                            _logger.LogWarning(
                                "⚠️ STREAM THROTTLED - SessionId: {SessionId}, Score: {Score}",
                                sessionId, session.SuspicionScore
                            );
                        }
                    }
                }
                else
                {
                    suspicionScore += 10;
                    reasons.Add("NoSessionId");
                }

                // 3. بررسی سبک‌تر Referer (رفع باگ حساسیت به حروف بزرگ و کوچک)
                if (string.IsNullOrEmpty(referer))
                {
                    suspicionScore += 5;
                    reasons.Add("NoReferer");
                }
                else if (!referer.Contains(context.Request.Host.Value, StringComparison.OrdinalIgnoreCase))
                {
                    suspicionScore += 15;
                    reasons.Add("InvalidReferer");
                }

                // 4. بررسی Accept Header 
                if (accept == "*/*" && string.IsNullOrEmpty(acceptLanguage))
                {
                    suspicionScore += 10;
                    reasons.Add("SimpleAccept");
                }

                // 5. بررسی Range Header pattern (هوشمندانه‌تر و امن‌تر در برابر فرمت‌های دستکاری شده)
                var range = context.Request.Headers["Range"].ToString();
                if (!string.IsNullOrEmpty(range) && range.StartsWith("bytes=0-", StringComparison.OrdinalIgnoreCase))
                {
                    var rangeEndString = range.Substring(8).Trim();

                    // جلوگیری از خطا در صورت ارسال Range های چندگانه (Multiple Ranges)
                    var firstRangeEnd = rangeEndString.Split(',')[0].Trim();

                    if (int.TryParse(firstRangeEnd, out int endByte))
                    {
                        if (endByte == 262143 || endByte == 524287 || endByte == 1048575)
                        {
                            suspicionScore += 20;
                            reasons.Add("SuspiciousRange");
                        }
                    }
                }

                // 6. مسدودسازی نهایی (رفع باگ حیاتی - اعمال جریمه در صورت رسیدن به حد آستانه)
                if (suspicionScore >= 80)
                {
                    _logger.LogError(
                        "🚫 BLOCKED by accumulated score - Score: {Score}, IP: {IP}, " +
                        "Reasons: [{Reasons}], UA: {UserAgent}",
                        suspicionScore, ipAddress, string.Join(", ", reasons), userAgent
                    );

                    context.Response.StatusCode = 403; // مسدود کردن درخواست
                    return; // جلوگیری از اجرای Middleware بعدی
                }
                else if (suspicionScore >= 40)
                {
                    _logger.LogWarning(
                        "⚠️ Suspicious activity detected - Score: {Score}, IP: {IP}, " +
                        "Reasons: [{Reasons}], UA: {UserAgent}",
                        suspicionScore, ipAddress, string.Join(", ", reasons), userAgent
                    );
                }
                else if (suspicionScore >= 20)
                {
                    _logger.LogInformation(
                        "ℹ️ Minor suspicion - Score: {Score}, IP: {IP}, Reasons: [{Reasons}]",
                        suspicionScore, ipAddress, string.Join(", ", reasons)
                    );
                }
            }

            // فقط در صورتی به اینجا می‌رسد که کاربر مسدود نشده باشد
            await _next(context);
        }
    }

    public static class AntiDownloadManagerMiddlewareExtensions
    {
        public static IApplicationBuilder UseAntiDownloadManager(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AntiDownloadManagerMiddleware>();
        }
    }
}