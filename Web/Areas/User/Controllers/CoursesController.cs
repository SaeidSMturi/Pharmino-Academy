using Application.Interfaces;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using Application.Streams;
using Web.Areas.User.Models; // فرض بر این است که مدل‌ها را اینجا قرار می‌دهید
using Web.Services.Pdf;

namespace Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class CoursesController : Controller
    {
        private readonly ICourseService _courseService;
        private readonly IWebHostEnvironment _environment;
        private readonly IVideoTokenService _videoTokenService;
        private readonly IVideoSessionManager _sessionManager;
        private readonly ILogger<CoursesController> _logger;
        private readonly IOneTimeUrlService _oneTimeUrlService;
        private readonly IIpBindingService _ipBindingService;
        private readonly IHlsProcessingService _hlsProcessingService;
        private readonly IDataProtector _protector;
        private readonly IMemoryCache _cache;
        private readonly IStorageService _storageService;
        private readonly IPdfFileLocator _pdfLocator;
        private readonly ISecurePdfRenderer _pdfRenderer;

        public CoursesController(
            ICourseService courseService, IWebHostEnvironment environment,
            IVideoTokenService videoTokenService, IVideoSessionManager sessionManager,
            ILogger<CoursesController> logger, IOneTimeUrlService oneTimeUrlService,
            IIpBindingService ipBindingService, IHlsProcessingService hlsProcessingService,
            IDataProtectionProvider provider, IMemoryCache cache, IStorageService storageService,
            IPdfFileLocator pdfLocator, ISecurePdfRenderer pdfRenderer)
        {
            _courseService = courseService;
            _environment = environment;
            _videoTokenService = videoTokenService;
            _sessionManager = sessionManager;
            _logger = logger;
            _oneTimeUrlService = oneTimeUrlService;
            _ipBindingService = ipBindingService;
            _hlsProcessingService = hlsProcessingService;
            _protector = provider.CreateProtector("SecurePdfStream_v1");
            _cache = cache;
            _storageService = storageService;
            _pdfLocator = pdfLocator;
            _pdfRenderer = pdfRenderer;
        }

        #region Helpers
        private int GetCurrentUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;

        private async Task<(bool IsValid, IActionResult ErrorResult, dynamic Video)> ValidateVideoAccessAsync(int videoId, int userId)
        {
            var video = await _courseService.GetVideoByIdAsync(videoId);
            if (video == null) return (false, NotFound(new { error = "Video not found" }), null);

            var canAccess = await _courseService.CanUserAccessVideoAsync(userId, videoId);
            if (!canAccess) return (false, Forbid(), null);

            return (true, null, video);
        }
        #endregion

        #region Page Views
        public async Task<IActionResult> Index()
        {
            var enrolledCourses = await _courseService.GetUserEnrolledCoursesAsync(GetCurrentUserId());
            return View(enrolledCourses);
        }

        [HttpGet("/User/Course/{id}")]
        public async Task<IActionResult> MyCourseDetails(int id)
        {
            var userId = GetCurrentUserId();
            var course = await _courseService.GetCourseWithDetailsAsync(id);

            if (course == null || !await _courseService.IsUserEnrolledAsync(userId, id))
            {
                TempData["ErrorMessage"] = course == null ? "Course not found" : "You are not enrolled in this course";
                return RedirectToAction(nameof(Index));
            }

            if (course.IsBundle)
            {
                var bundleCourses = await _courseService.GetBundleCoursesAsync(id);
                ViewBag.BundleCourses = bundleCourses;
                ViewBag.Enrollment = await _courseService.GetUserEnrollmentAsync(userId, id);
                return View("BundleDetails", course);
            }

            ViewBag.Videos = await _courseService.GetCourseVideosAsync(id, userId);
            ViewBag.Pdfs = await _courseService.GetCoursePdfsAsync(id, userId);
            ViewBag.Enrollment = await _courseService.GetUserEnrollmentAsync(userId, id);
            ViewBag.VideoProgress = await _courseService.GetVideoProgressDictAsync(userId, id);

            return View(course);
        }

        public async Task<IActionResult> Watch(int videoId)
        {
            var userId = GetCurrentUserId();
            var (isValid, errorResult, video) = await ValidateVideoAccessAsync(videoId, userId);
            if (!isValid)
            {
                TempData["ErrorMessage"] = "Video not found or access denied.";
                return RedirectToAction(nameof(Index));
            }

            var videoGuid = Path.GetFileNameWithoutExtension((string)video.VideoFileName);

            ViewBag.VideoToken = _videoTokenService.GenerateToken(videoId, userId, 60);
            ViewBag.SessionId = _videoTokenService.GenerateSessionId(videoId, userId);
            ViewBag.CourseTitle = video.Course.Title;
            ViewBag.CourseId = video.CourseId;
            ViewBag.UserId = userId;
            ViewBag.HlsAvailable = await _hlsProcessingService.HlsExistsAsync((int)video.CourseId, videoGuid);
            ViewBag.VideoGuid = videoGuid;

            return View(video);
        }
        #endregion

        #region Video Security & Streaming (HLS & Direct)
        [HttpPost]
        public IActionResult GetChunkToken([FromBody] ChunkTokenRequest request)
        {
            if (string.IsNullOrEmpty(request?.SessionId)) return BadRequest(new { error = "Invalid request" });

            var userId = GetCurrentUserId();
            try
            {
                if (!_videoTokenService.ValidateSessionId(request.SessionId, request.VideoId, userId))
                {
                    _logger.LogWarning("Invalid session for chunk token - UserId: {UserId}, VideoId: {VideoId}", userId, request.VideoId);
                    return Unauthorized(new { error = "Invalid session" });
                }

                return Ok(new { success = true, token = _videoTokenService.GenerateOneTimeStreamToken(request.VideoId, userId, request.SessionId), expiresIn = 30 });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Chunk token rate limit - Error: {Error}", ex.Message);
                return StatusCode(429, new { error = "Too many requests. Please slow down." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetOneTimeStreamUrl([FromBody] OneTimeUrlRequest request)
        {
            var userId = GetCurrentUserId();
            var ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            if (_ipBindingService.IsSuspiciousIpPattern(userId, ipAddress))
            {
                _logger.LogError("BLOCKED: Suspicious IP pattern - UserId: {UserId}, IP: {IP}", userId, ipAddress);
                return StatusCode(429, new { error = "Suspicious activity detected" });
            }

            var (isValid, errorResult, _) = await ValidateVideoAccessAsync(request.VideoId, userId);
            if (!isValid) return errorResult;

            var oneTimeUrl = _oneTimeUrlService.GenerateOneTimeUrl(request.VideoId, userId, request.SessionId, ttlSeconds: 10);
            return Ok(new
            {
                success = true,
                url = Url.Action(nameof(StreamWithOneTimeUrl), "Courses", new { Area = "User", videoId = request.VideoId, otUrl = oneTimeUrl }, Request.Scheme),
                expiresIn = 10
            });
        }

        [HttpGet]
        public async Task<IActionResult> StreamWithOneTimeUrl(int videoId, string otUrl)
        {
            var userId = GetCurrentUserId();
            var (isValidUrl, sessionId) = ExtractSessionFromOneTimeUrl(otUrl);

            if (!isValidUrl || !_oneTimeUrlService.ValidateAndConsumeUrl(otUrl, videoId, userId, sessionId))
                return StatusCode(403, new { error = "Invalid or expired URL" });

            var (isValidAccess, errorResult, video) = await ValidateVideoAccessAsync(videoId, userId);
            if (!isValidAccess) return errorResult;

            var videoPath = Path.Combine(_environment.WebRootPath, "uploads", "courses", video.CourseId.ToString(), "videos", video.VideoFileName);
            if (!System.IO.File.Exists(videoPath)) return NotFound();

            var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true);

            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            Response.Headers.Append("Cache-Control", "private, no-cache, no-store, must-revalidate");

            return File(fileStream, "video/mp4", enableRangeProcessing: true);
        }

        [HttpGet]
        public async Task<IActionResult> GetVideoMetadata(int videoId, string token)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(token) || !_videoTokenService.ValidateToken(token, videoId, userId))
                return Json(new { success = false, error = "Invalid token" });

            var (isValid, errorResult, video) = await ValidateVideoAccessAsync(videoId, userId);
            if (!isValid) return Json(new { success = false, error = "Access denied" });

            return Json(new
            {
                success = true,
                videoId = video.VideoId,
                title = video.Title,
                duration = video.DurationSeconds,
                streamUrl = Url.Action(nameof(StreamVideo), "Courses", new { Area = "User", videoId, token }),
                expiresAt = DateTime.UtcNow.AddMinutes(5).ToString("o")
            });
        }

        public async Task<IActionResult> StreamVideo(int videoId, string token, string sid = null, string ct = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

                // 1. بررسی سشن و توکن ها
                if (string.IsNullOrWhiteSpace(sid) || !_videoTokenService.ValidateSessionId(sid, videoId, userId))
                    return Unauthorized(new { error = "Invalid or missing session ID" });

                if (!string.IsNullOrEmpty(ct))
                {
                    var (isValidChunkToken, chunkTokenError) = _videoTokenService.ValidateAndConsumeOneTimeToken(ct, videoId, userId, sid);
                    if (!isValidChunkToken)
                    {
                        var session = _sessionManager.GetSession(sid);
                        if (session != null) session.SuspicionReasons["InvalidChunkToken"] = 15;
                    }
                }

                if (string.IsNullOrEmpty(token) || !_videoTokenService.ValidateToken(token, videoId, userId))
                    return Unauthorized(new { error = "Invalid or expired token" });

                // 2. بررسی دسترسی کاربر به ویدیو
                var (isValid, errorResult, video) = await ValidateVideoAccessAsync(videoId, userId);
                if (!isValid) return errorResult;

                var directoryPath = $"uploads/courses/{video.CourseId}/videos";

                // ❌ خطوط زیر حذف شدند تا سرعت پاسخ دهی افزایش یابد:
                // if (!await _storageService.FileExistsAsync(directoryPath, video.VideoFileName))
                //     return NotFound(new { error = "Video file not found" });

                // 3. اجرای مستقیم پروسه استریم
                try
                {
                    return await ProcessVideoStreamAsync(video, sid, userId, ipAddress, directoryPath);
                }
                catch (FileNotFoundException)
                {
                    // مدیریت خطای 404 در صورتی که فایل در DigitalOcean وجود نداشت
                    return NotFound(new { error = "Video file not found" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR in StreamVideo - VideoId: {VideoId}", videoId);
                return StatusCode(500, new { error = "Video streaming error" });
            }
        }

        #endregion

        #region Telemetry & Analytics
        [HttpPost]
        public IActionResult PlayerHeartbeat([FromBody] HeartbeatRequest request)
        {
            if (string.IsNullOrEmpty(request?.SessionId)) return BadRequest(new { error = "SessionId is required" });

            _sessionManager.UpdateHeartbeat(request.SessionId, request.PlayerState, request.CurrentPosition, request.Duration, request.BufferHealth);
            var session = _sessionManager.GetSession(request.SessionId);

            return Ok(new
            {
                success = true,
                suspicionScore = session?.SuspicionScore ?? 0,
                isBlocked = session?.IsBlocked ?? false,
                isThrottled = session?.IsThrottled ?? false,
                currentBandwidthKBps = _sessionManager.GetCurrentBandwidthLimit(request.SessionId)
            });
        }

        [HttpPost]
        public IActionResult ValidateFingerprint([FromBody] FingerprintRequest request)
        {
            // منطق اثر انگشت دست‌نخورده باقی مانده اما کد تمیزتر شده است
            if (request?.Fingerprint == null) return Ok(new { success = false, isBlocked = false, warning = "Fingerprint required" });

            var fp = request.Fingerprint;
            int riskScore = CalculateFingerprintRiskScore(fp, out var flags);

            if (riskScore >= 80)
                _logger.LogError("BLOCKED by Fingerprint - UserId: {UserId}, Score: {Score}", GetCurrentUserId(), riskScore);

            return Ok(new { success = true, riskScore, isBlocked = riskScore >= 80, flags });
        }

        [HttpPost]
        public IActionResult ReportSuspiciousActivity([FromBody] SuspiciousActivityReport report)
        {
            var severityScore = report?.Type switch
            {
                "SCREEN_RECORDING_API_CALLED" => 50,
                "SCREEN_RECORDING_EXTENSION" => 40,
                "PRINT_SCREEN" => 35,
                "DEVTOOLS_OPENED" => 30,
                "DEVTOOLS_SHORTCUT" => 25,
                "PIP_ACTIVATED" => 20,
                "F12_PRESSED" => 15,
                "LOW_FRAME_RATE" => 10,
                _ => 5
            };

            return Ok(new { success = true, recorded = true, severityScore });
        }

        [HttpPost]
        public async Task<IActionResult> SaveVideoProgress([FromBody] VideoProgressRequest request)
        {
            if (request == null || request.VideoId <= 0) return BadRequest(new { success = false });

            var userId = GetCurrentUserId();
            var (isValid, errorResult, _) = await ValidateVideoAccessAsync(request.VideoId, userId);
            if (!isValid) return errorResult;

            await _courseService.SaveVideoProgressAsync(userId, request.VideoId, request.Position, request.Duration, request.Completed);
            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetVideoProgress(int videoId)
        {
            if (videoId <= 0) return BadRequest(new { success = false });

            var userId = GetCurrentUserId();
            var (isValid, errorResult, _) = await ValidateVideoAccessAsync(videoId, userId);
            if (!isValid) return errorResult;

            var progress = await _courseService.GetVideoProgressAsync(userId, videoId);
            return Ok(new
            {
                success = true,
                lastPosition = progress?.LastPosition ?? 0,
                duration = progress?.Duration ?? 0,
                completed = progress?.Completed ?? false,
                watchedPercent = progress?.WatchedPercent ?? 0
            });
        }
        #endregion

        #region PDF Operations
        [HttpGet("/User/Course/Pdf/{pdfId}")]
        public async Task<IActionResult> ViewPdf(int pdfId)
        {
            var userId = GetCurrentUserId();
            var pdf = await _courseService.GetPdfByIdAsync(pdfId);

            if (pdf == null || !await _courseService.CanUserAccessPdfAsync(userId, pdfId))
            {
                TempData["ErrorMessage"] = "PDF not found or access denied.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CourseTitle = pdf.Course.Title;
            ViewBag.CourseId = pdf.CourseId;
            ViewBag.PdfTitle = pdf.Title ?? pdf.OriginalFileName;

            var userIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "UnknownIP";
            var expiry = DateTimeOffset.UtcNow.AddHours(4).ToUnixTimeSeconds();
            ViewBag.StreamToken = _protector.Protect($"{pdfId}|{userId}|{userIp}|{expiry}");

            return View(pdf);
        }

        /// <summary>
        /// Returns page count + forensic code for the viewer. The original PDF is never sent to the browser.
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> PdfInfo(string token)
        {
            var (ok, error, pdfPath, viewer) = await ValidatePdfTokenAsync(token);
            if (!ok) return error!;

            try
            {
                var pageCount = _pdfRenderer.GetPageCount(pdfPath!);
                var code = _pdfRenderer.GetForensicCode(viewer!);

                _logger.LogInformation(
                    "PDF watermark issued - Code: {Code}, UserId: {UserId}, Email: {Email}, PdfId: {PdfId}, IP: {IP}",
                    code, viewer!.UserId, viewer.Email, viewer.PdfId, viewer.IpAddress);

                return Ok(new { pageCount, code, who = SecurePdfRenderer.MaskEmail(viewer.Email) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PdfInfo failed - PdfId: {PdfId}", viewer?.PdfId);
                return StatusCode(500, new { error = "Could not open document" });
            }
        }

        /// <summary>
        /// Returns ONE page as a JPEG with the user's forensic watermark burned into the pixels.
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> PdfPage(string token, int page)
        {
            var (ok, error, pdfPath, viewer) = await ValidatePdfTokenAsync(token);
            if (!ok) return error!;

            if (IsPdfRateLimited(viewer!.PdfId, viewer.UserId, viewer.IpAddress))
                return StatusCode(429, "تعداد درخواست‌ها بیش از حد مجاز است.");

            try
            {
                var day = DateTime.UtcNow.ToString("yyyyMMdd");
                var cacheKey = $"securepdf:page:{viewer.PdfId}:{page}:{viewer.UserId}:{day}";

                if (!_cache.TryGetValue(cacheKey, out byte[]? jpeg) || jpeg == null)
                {
                    jpeg = await Task.Run(() => _pdfRenderer.RenderPage(pdfPath!, page, viewer));
                    _cache.Set(cacheKey, jpeg, new MemoryCacheEntryOptions
                    {
                        Size = jpeg.Length,
                        SlidingExpiration = TimeSpan.FromMinutes(10)
                    });
                }

                SetStreamingSecurityHeaders();
                Response.Headers["Content-Disposition"] = "inline";
                return File(jpeg, "image/jpeg");
            }
            catch (ArgumentOutOfRangeException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PdfPage render failed - PdfId: {PdfId}, Page: {Page}", viewer.PdfId, page);
                return StatusCode(500, new { error = "Could not render page" });
            }
        }

        /// <summary>
        /// Shared checks for PdfInfo/PdfPage: token signature, expiry, IP, same user, enrollment, file on disk.
        /// </summary>
        private async Task<(bool Ok, IActionResult? Error, string? PdfPath, PdfViewer? Viewer)> ValidatePdfTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token)) return (false, Forbid(), null, null);

            int pdfId, tokenUserId;
            string expectedIp;
            long expiryTime;
            try
            {
                var parts = _protector.Unprotect(token).Split('|');
                if (parts.Length != 4) return (false, Forbid(), null, null);
                pdfId = int.Parse(parts[0]);
                tokenUserId = int.Parse(parts[1]);
                expectedIp = parts[2];
                expiryTime = long.Parse(parts[3]);
            }
            catch
            {
                return (false, Forbid(), null, null);
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiryTime)
                return (false, Unauthorized("لینک استریم منقضی شده است."), null, null);

            var currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "UnknownIP";
            if (currentIp != expectedIp)
                return (false, Unauthorized("دسترسی غیرمجاز. IP شما تغییر کرده است."), null, null);

            var userId = GetCurrentUserId();
            if (userId == 0 || userId != tokenUserId) return (false, Forbid(), null, null);

            var pdf = await _courseService.GetPdfByIdAsync(pdfId);
            if (pdf == null || !await _courseService.CanUserAccessPdfAsync(userId, pdfId))
                return (false, Forbid(), null, null);

            var pdfPath = _pdfLocator.Resolve(pdf.CourseId, pdf.FileName);
            if (pdfPath == null) return (false, NotFound(), null, null);

            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email)) email = User.Identity?.Name ?? $"user{userId}";

            return (true, null, pdfPath, new PdfViewer(userId, email, pdfId, currentIp));
        }
        #endregion

        #region Private Utility Methods
        private (bool isValid, string sessionId) ExtractSessionFromOneTimeUrl(string otUrl)
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(otUrl));
                var parts = decoded.Split(':');
                if (parts.Length >= 4) return (true, parts[3]);
            }
            catch { }
            return (false, null);
        }

        private async Task<IActionResult> ProcessVideoStreamAsync(dynamic video, string sid, int userId, string ipAddress, string directoryPath)
        {
            var existingSession = _sessionManager.GetSession(sid);
            if (existingSession == null && !_sessionManager.CanStartStream(userId, (int)video.VideoId))
                return StatusCode(429, new { error = "Too many concurrent streams" });

            _sessionManager.StartStream(userId, (int)video.VideoId, sid, ipAddress, Request.Headers["User-Agent"].ToString());

            if (existingSession != null)
            {
                var (shouldBlock, _, realTimeReason) = existingSession.CheckRealTimeThreats();
                if (shouldBlock) return StatusCode(403, new { error = "Stream blocked", reason = realTimeReason });
            }

            long totalFileSize = await _storageService.GetFileSizeAsync(directoryPath, (string)video.VideoFileName);
            long maxChunkSize = 4 * 1024 * 1024; // محدودیت 4 مگابایت

            long? rangeStart = null;
            long? rangeEnd = null;

            // 1. هوشمندسازی درخواست Range
            if (Request.Headers.TryGetValue("Range", out var rangeHeader) && TryParseRange(rangeHeader, out var start, out var end))
            {
                rangeStart = start;

                // اگر مرورگر بازه پایان (end) را مشخص نکرده بود یا بیشتر از 4 مگابایت خواسته بود، ما آن را اصلاح می‌کنیم
                long requestedEnd = end > 0 ? end : (totalFileSize - 1);
                rangeEnd = Math.Min(start + maxChunkSize - 1, requestedEnd);

                // 💡 ترفند طلایی: هدر درخواست مرورگر را در پرواز بازنویسی می‌کنیم
                Request.Headers["Range"] = $"bytes={rangeStart}-{rangeEnd}";

                _sessionManager.GetSession(sid)?.RecordRangeRequest(start, end);
            }

            // 2. دریافت استریم فقط برای بازه محاسبه شده از S3
            var (fileStream, contentLength) = await _storageService.GetFileStreamAsync(directoryPath, (string)video.VideoFileName, rangeStart, rangeEnd);

            var throttledStream = new ThrottledVideoStream(fileStream, _sessionManager, sid, _logger);

            SetStreamingSecurityHeaders();

            // 3. اجازه می‌دهیم ASP.NET Core خودش هدرهای 206، Content-Length و اتصال‌ها را مدیریت کند
            return File(throttledStream, "video/mp4", enableRangeProcessing: true);
        }
        private void SetStreamingSecurityHeaders()
        {
            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
            Response.Headers.Append("Cache-Control", "private, no-cache, no-store, must-revalidate");
            Response.Headers.Append("Pragma", "no-cache");
            Response.Headers.Append("Expires", "0");
        }

        private bool TryParseRange(string rangeHeader, out long start, out long end)
        {
            start = end = 0;
            if (string.IsNullOrWhiteSpace(rangeHeader) || !rangeHeader.StartsWith("bytes=")) return false;
            var range = rangeHeader.Substring(6).Split('-');
            return range.Length == 2 && long.TryParse(range[0], out start) && long.TryParse(range[1], out end);
        }

        private bool IsPdfRateLimited(int pdfId, int userId, string ip)
        {
            // ~90 page images per minute per user+PDF+IP: normal reading never gets close,
            // scripted bulk scraping does. Size is required because AddMemoryCache sets SizeLimit.
            string cacheKey = $"RateLimit_PdfPage_{pdfId}_U{userId}_IP_{ip}";
            var count = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                entry.Size = 1;
                return new int[1];
            })!;
            return Interlocked.Increment(ref count[0]) > 90;
        }

        private int CalculateFingerprintRiskScore(BrowserFingerprintDto fp, out List<string> flags)
        {
            int riskScore = 0;
            flags = new List<string>();
            var ua = (fp.UserAgent ?? "").ToLower();

            string[] suspiciousPatterns = { "idm", "wget", "curl", "aria2", "python", "java/", "bot", "crawler" };
            foreach (var pattern in suspiciousPatterns)
                if (ua.Contains(pattern)) { riskScore += 50; flags.Add($"SUSPICIOUS_UA:{pattern}"); }

            if (fp.HasAutomationFlags) { riskScore += 40; flags.Add("AUTOMATION"); }
            if (fp.DevToolsOpen) { riskScore += 20; flags.Add("DEVTOOLS"); }
            if (string.IsNullOrEmpty(fp.CanvasHash)) { riskScore += 25; flags.Add("NO_CANVAS"); }

            if (string.IsNullOrEmpty(fp.WebGLRenderer) || fp.WebGLRenderer.Contains("SwiftShader") || fp.WebGLRenderer.Contains("Mesa"))
            { riskScore += 30; flags.Add("VIRTUAL_GPU"); }

            if (fp.HardwareConcurrency == 0) { riskScore += 15; flags.Add("NO_HARDWARE"); }

            return riskScore;
        }
        #endregion
    }
}

// ==========================================
// فایل‌های زیر بهتر است به پوشه Models/DTOs منتقل شوند:
// ==========================================
namespace Web.Areas.User.Models
{
    public class ChunkTokenRequest { public int VideoId { get; set; } public string SessionId { get; set; } }
    public class OneTimeUrlRequest { public int VideoId { get; set; } public string SessionId { get; set; } }
    public class HeartbeatRequest { public string SessionId { get; set; } public string PlayerState { get; set; } public double CurrentPosition { get; set; } public double Duration { get; set; } public double BufferHealth { get; set; } }
    public class FingerprintRequest { public string SessionId { get; set; } public BrowserFingerprintDto Fingerprint { get; set; } }
    public class BrowserFingerprintDto { public string UserAgent { get; set; } public string Platform { get; set; } public string[] Languages { get; set; } public int ScreenWidth { get; set; } public int ScreenHeight { get; set; } public string CanvasHash { get; set; } public string WebGLVendor { get; set; } public string WebGLRenderer { get; set; } public int HardwareConcurrency { get; set; } public double DeviceMemory { get; set; } public bool DevToolsOpen { get; set; } public bool HasAutomationFlags { get; set; } public string ClientHash { get; set; } }
    public class SuspiciousActivityReport { public string SessionId { get; set; } public int UserId { get; set; } public string Type { get; set; } public Dictionary<string, object> Data { get; set; } public string Timestamp { get; set; } }
    public class VideoProgressRequest { public int VideoId { get; set; } public double Position { get; set; } public double Duration { get; set; } public bool Completed { get; set; } }
}