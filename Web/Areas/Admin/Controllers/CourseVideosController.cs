using Domain.Configuration;
using Application.Interfaces;
using Infrastructure.Persistence;
using Domain.Entities.Course;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class CourseVideosController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly UploadLimitsOptions _uploadLimits;
        private readonly IHlsProcessingService _hlsService;
        private readonly IHlsProgressService _hlsProgress;
        private readonly ILogger<CourseVideosController> _logger;
        private readonly IStorageService _storageService;


        private const string TempUploadSessionPrefix = "CourseVideos.TempUpload.";

        public class IdRequest
        {
            public int Id { get; set; }
        }

        public class UploadVideoResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public string? Token { get; set; }
            public string? FileName { get; set; }
        }

        // توزیع و تزریق از قبل در کانستراکتور انجام شده بود، از همان نمونه استفاده می‌کنیم
        public CourseVideosController(
            WebContext context,
            IWebHostEnvironment environment,
            IOptions<UploadLimitsOptions> uploadLimits,
            IHlsProcessingService hlsService,
            IHlsProgressService hlsProgress,
            ILogger<CourseVideosController> logger, IStorageService storageService)
        {
            _context = context;
            _environment = environment;
            _uploadLimits = uploadLimits.Value;
            _hlsService = hlsService;
            _hlsProgress = hlsProgress;
            _logger = logger;
            _storageService = storageService;
        }

        [HttpPost]
        [Permission("Courses.Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadVideo(int courseId, IFormFile? videoFile)
        {
            var file = videoFile ?? Request.Form.Files.FirstOrDefault();
            _logger.LogInformation("AJAX Video chunk/file upload initiated for Course ID: {CourseId}. Name: {FileName}, Size: {Length} bytes",
                courseId, file?.FileName, file?.Length);

            if (courseId <= 0)
            {
                _logger.LogWarning("AJAX upload rejected. Invalid Course ID: {CourseId}", courseId);
                return Json(new UploadVideoResponse { Success = false, Message = "Invalid course id" });
            }

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("AJAX upload rejected. File is empty or null for Course ID: {CourseId}", courseId);
                return Json(new UploadVideoResponse { Success = false, Message = "Please select a video file" });
            }

            if (file.Length > _uploadLimits.CourseVideoMaxBytes)
            {
                _logger.LogWarning("AJAX upload rejected. File size {Length} exceeds limit {Limit} for Course ID: {CourseId}",
                    file.Length, _uploadLimits.CourseVideoMaxBytes, courseId);
                return Json(new UploadVideoResponse { Success = false, Message = "Video size exceeds the allowed limit" });
            }

            var allowedExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                _logger.LogWarning("AJAX upload rejected. Extension '{Extension}' not supported for Course ID: {CourseId}", fileExtension, courseId);
                return Json(new UploadVideoResponse { Success = false, Message = "Only video files are allowed" });
            }

            // تعیین مسیر استاندارد دایرکتوری (چه برای لوکال چه برای باکت)
            var directoryPath = $"uploads/courses/{courseId}/videos";
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

            try
            {
                // استفاده از سرویس ذخیره‌ساز یکپارچه برای آپلود فایل
                await _storageService.UploadFileAsync(file, directoryPath, uniqueFileName);
                _logger.LogInformation("Video file successfully uploaded via StorageService to: {FilePath}", $"{directoryPath}/{uniqueFileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload video file via StorageService during AJAX upload. Path: {FilePath}", $"{directoryPath}/{uniqueFileName}");

                // تلاش برای پاکسازی فایل در صورت خطای ذخیره‌سازی
                try
                {
                    await _storageService.DeleteFileAsync(directoryPath, uniqueFileName);
                }
                catch { /* Ignore */ }

                return Json(new UploadVideoResponse { Success = false, Message = "Failed to upload video" });
            }

            var token = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString(TempUploadSessionPrefix + token, uniqueFileName);
            _logger.LogInformation("Temporary video upload tracked in session container. Token: {SessionToken} -> File: {UniqueName}", token, uniqueFileName);

            return Json(new UploadVideoResponse { Success = true, Token = token, FileName = uniqueFileName });
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ⚠️ MOCK / TEST METHOD ONLY ⚠️
        // این متد صرفاً جهت تست دانلود ویدیو از سطح اینترنت و شبیه‌سازی آپلود است
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [HttpPost]
        [Permission("Courses.Create")]
        public async Task<IActionResult> TestOnly_DownloadVideoFromUrl(int courseId, string videoUrl)
        {
            _logger.LogWarning("⚠️ TEST METHOD EXECUTED: Attempting to download test video from {Url} for Course ID: {CourseId}", videoUrl, courseId);

            if (courseId <= 0)
                return BadRequest(new { success = false, message = "شناسه دوره نامعتبر است." });

            if (string.IsNullOrWhiteSpace(videoUrl) || !Uri.TryCreate(videoUrl, UriKind.Absolute, out var uriResult))
                return BadRequest(new { success = false, message = "آدرس اینترنتی (URL) نامعتبر است." });

            try
            {
                // ۱. دانلود ویدیو از آدرس اینترنتی
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(uriResult, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("⚠️ TEST METHOD FAILED: Cannot download from {Url}. Status: {StatusCode}", videoUrl, response.StatusCode);
                    return BadRequest(new { success = false, message = "دانلود ویدیو از آدرس اینترنتی با خطا مواجه شد." });
                }

                var fileExtension = Path.GetExtension(uriResult.AbsolutePath).ToLowerInvariant();
                if (string.IsNullOrEmpty(fileExtension) || !fileExtension.Contains("."))
                    fileExtension = ".mp4";

                var uniqueFileName = $"TEST_{Guid.NewGuid()}{fileExtension}";
                var directoryPath = $"uploads/courses/{courseId}/videos";

                // ۲. کپی کردن استریم شبکه درون MemoryStream برای جلوگیری از کرش
                using var networkStream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await networkStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0; // مهم: بازگرداندن نشانگر خواندن به ابتدای فایل

                // ۳. ساخت FormFile امن و قابل پردازش
                var formFile = new FormFile(memoryStream, 0, memoryStream.Length, "testVideoFile", uniqueFileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = response.Content.Headers.ContentType?.MediaType ?? "video/mp4"
                };

                // ۴. آپلود فایل با استفاده از سرویس ذخیره‌ساز فعلی سیستم
                await _storageService.UploadFileAsync(formFile, directoryPath, uniqueFileName);
                _logger.LogInformation("⚠️ TEST METHOD: Downloaded file saved to {Path}", $"{directoryPath}/{uniqueFileName}");

                // ۵. ذخیره متادیتا در دیتابیس
                var newVideo = new CourseVideo
                {
                    CourseId = courseId,
                    Title = "تست دانلود ویدیو (Test)",
                    Description = $"این ویدیو در تاریخ {DateTime.Now} مستقیماً از سطح اینترنت دانلود و ذخیره شده است.",
                    VideoFileName = uniqueFileName,
                    IsFree = true,
                    IsActive = true,
                    OrderIndex = _context.CourseVideos.Count(v => v.CourseId == courseId) + 1, // گرفتن ترتیب درست
                    CreateDate = DateTime.UtcNow
                };

                _context.CourseVideos.Add(newVideo);
                await _context.SaveChangesAsync();

                // آپدیت کردن مدت زمان دوره (طبق روال استاندارد کنترلر)
                await UpdateCourseDurationAsync(courseId);

                _logger.LogInformation("⚠️ TEST METHOD SUCCESS: Video ID {VideoId} created successfully.", newVideo.VideoId);

                return Ok(new { success = true, message = "ویدیوی تست با موفقیت دانلود و ذخیره شد.", videoId = newVideo.VideoId });
            }
            catch (Exception ex)
            {
                // در کنسول (ترمینال) محیط توسعه یا سرور، خطای دقیق در اینجا چاپ می‌شود
                _logger.LogError(ex, "⚠️ TEST METHOD CRASH: Unexpected error during download/save process.");
                return StatusCode(500, new { success = false, message = "خطای سرور هنگام پردازش ویدیوی تست." });
            }
        }
        private async Task UpdateCourseDurationAsync(int courseId)
        {
            _logger.LogInformation("Recalculating absolute duration metadata for Course ID: {CourseId}", courseId);

            var course = await _context.Courses
                .Include(c => c.Videos)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null)
            {
                _logger.LogWarning("Duration calculation skipped. Course ID {CourseId} not found in repository.", courseId);
                return;
            }

            var totalSeconds = course.Videos?
                .Where(v => v.IsActive && v.DurationSeconds.HasValue)
                .Sum(v => v.DurationSeconds.Value) ?? 0;

            var oldDuration = course.DurationHours;
            course.DurationHours = totalSeconds > 0 ? (int)Math.Ceiling(totalSeconds / 3600.0) : 0;
            course.LastUpdate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Course ID {CourseId} duration updated. Total Seconds: {Seconds}, Total Ceiled Hours: {Hours} (Was: {OldHours})",
                courseId, totalSeconds, course.DurationHours, oldDuration);
        }

        [Permission("Courses.Create")]
        public IActionResult Create(int courseId)
        {
            _logger.LogInformation("Navigating to video creation page for Course ID: {CourseId}", courseId);

            var course = _context.Courses.FirstOrDefault(c => c.CourseId == courseId);
            if (course == null)
            {
                _logger.LogWarning("Creation view aborted. Target Course ID {CourseId} not found.", courseId);
                TempData["ErrorMessage"] = "Course not found";
                return RedirectToAction("Index", "Courses");
            }

            ViewBag.CourseId = courseId;
            ViewBag.CourseTitle = course.Title;
            return View();
        }

        [HttpPost]
        [Permission("Courses.Create")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10737418240)] // 10GB for video upload
        public async Task<IActionResult> Create([Bind(nameof(CourseVideo.CourseId), nameof(CourseVideo.Title), nameof(CourseVideo.Description), nameof(CourseVideo.DurationSeconds), nameof(CourseVideo.OrderIndex), nameof(CourseVideo.IsFree), nameof(CourseVideo.IsActive))] CourseVideo video, IFormFile? VideoFile, string? UploadedVideoToken)
        {
            _logger.LogInformation("Form submission received to submit new video for Course ID: {CourseId}. Token Present: {TokenPresent}",
                video.CourseId, !string.IsNullOrWhiteSpace(UploadedVideoToken));

            video.Title = video.Title?.Trim();
            video.Description = video.Description?.Trim();

            ModelState.Remove("VideoFileName");
            ModelState.Remove("Course");
            ModelState.Remove("CreateDate");
            ModelState.Remove("LastUpdate");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("Video submit aborted due to invalid model state for Course ID: {CourseId}. Errors: {Errors}", video.CourseId, errors);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    TempData["ErrorMessage"] = errors;
                }

                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                if (course != null)
                {
                    ViewBag.CourseId = video.CourseId;
                    ViewBag.CourseTitle = course.Title;
                }
                return View(video);
            }

            // مسیر دایرکتوری استاندارد برای ذخیره‌سازی (چه لوکال چه باکت)
            var directoryPath = $"uploads/courses/{video.CourseId}/videos";

            // Prefer previously uploaded video
            if (!string.IsNullOrWhiteSpace(UploadedVideoToken))
            {
                var sessionKey = TempUploadSessionPrefix + UploadedVideoToken;
                var uploadedFileName = HttpContext.Session.GetString(sessionKey);
                if (!string.IsNullOrWhiteSpace(uploadedFileName))
                {
                    video.VideoFileName = uploadedFileName;
                    HttpContext.Session.Remove(sessionKey);
                    _logger.LogInformation("Resolved video filename from temporary session token setup. Assigned: {FileName}", uploadedFileName);
                }
                else
                {
                    _logger.LogWarning("Token '{SessionToken}' was provided but no file lookup matches in session repository.", UploadedVideoToken);
                }
            }

            // Fallback (if token not used) to old behavior
            if (string.IsNullOrWhiteSpace(video.VideoFileName))
            {
                _logger.LogInformation("Fallback path triggered. Processing direct standard upload file.");
                if (VideoFile == null || VideoFile.Length == 0)
                {
                    _logger.LogWarning("Creation failed. Direct Video file is null or completely empty.");
                    TempData["ErrorMessage"] = "Please select a video file";
                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                    if (course != null)
                    {
                        ViewBag.CourseId = video.CourseId;
                        ViewBag.CourseTitle = course.Title;
                    }
                    return View(video);
                }

                if (VideoFile.Length > _uploadLimits.CourseVideoMaxBytes)
                {
                    _logger.LogWarning("Creation failed. Direct file size {Size} exceeds boundary limit {Limit}", VideoFile.Length, _uploadLimits.CourseVideoMaxBytes);
                    TempData["ErrorMessage"] = "Video size exceeds the allowed limit";
                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                    if (course != null)
                    {
                        ViewBag.CourseId = video.CourseId;
                        ViewBag.CourseTitle = course.Title;
                    }
                    return View(video);
                }

                var allowedExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" };
                var fileExtension = Path.GetExtension(VideoFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Creation failed. Direct fallback file format '{Extension}' is unauthorized.", fileExtension);
                    TempData["ErrorMessage"] = "Only video files are allowed";
                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                    if (course != null)
                    {
                        ViewBag.CourseId = video.CourseId;
                        ViewBag.CourseTitle = course.Title;
                    }
                    return View(video);
                }

                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

                try
                {
                    // استفاده از سرویس ذخیره‌ساز یکپارچه
                    await _storageService.UploadFileAsync(VideoFile, directoryPath, uniqueFileName);
                    video.VideoFileName = uniqueFileName;
                    _logger.LogInformation("Direct synchronous file stream write finalized using StorageService to: {Path}", $"{directoryPath}/{uniqueFileName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write direct fallback file to StorageService.");
                    TempData["ErrorMessage"] = "Server storage error committing video stream.";
                    return View(video);
                }
            }

            video.CreateDate = DateTime.UtcNow;

            try
            {
                _context.CourseVideos.Add(video);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Video entity persisted to DB infrastructure. Video ID: {VideoId}, File: {FileName}", video.VideoId, video.VideoFileName);

                await UpdateCourseDurationAsync(video.CourseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database save failed for Video object metadata. Commencing file structure self-cleanup.");
                TempData["ErrorMessage"] = $"Failed to save video: {ex.Message}";

                if (!string.IsNullOrWhiteSpace(video.VideoFileName))
                {
                    try
                    {
                        // استفاده از سرویس حذف یکپارچه
                        await _storageService.DeleteFileAsync(directoryPath, video.VideoFileName);
                        _logger.LogInformation("Cleanup complete: Reaped unreferenced video file.");
                    }
                    catch (Exception delEx) { _logger.LogError(delEx, "Cleanup crash: Unable to purge file using StorageService."); }
                }

                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                if (course != null)
                {
                    ViewBag.CourseId = video.CourseId;
                    ViewBag.CourseTitle = course.Title;
                }
                return View(video);
            }

            TempData["SuccessMessage"] = "Video added successfully.";
            return RedirectToAction("Details", "Courses", new { Area = "Admin", id = video.CourseId });
        }

        [HttpPost]
        [Permission("Courses.Delete")]
        public async Task<IActionResult> Delete([FromBody] IdRequest? payload, int? id)
        {
            var videoId = payload?.Id > 0 ? payload!.Id : (id ?? 0);
            _logger.LogWarning("Destruction routine initiated for Video ID: {VideoId} by Admin Account.", videoId);

            try
            {
                if (videoId <= 0)
                {
                    _logger.LogWarning("Delete rejected. Invalid Video ID input: {VideoId}", videoId);
                    return Json(new { success = false, message = "Invalid video id" });
                }

                var video = await _context.CourseVideos.FirstOrDefaultAsync(v => v.VideoId == videoId);
                if (video == null)
                {
                    _logger.LogWarning("Delete execution failed. Video entity matching ID {VideoId} was missing from EF Context.", videoId);
                    return Json(new { success = false, message = "Video not found" });
                }

                // حذف فایل ویدیو در صورت وجود
                if (!string.IsNullOrEmpty(video.VideoFileName))
                {
                    var videoFolder = Path.Combine(_environment.WebRootPath, "uploads", "courses", video.CourseId.ToString(), "videos");
                    var videoPath = Path.Combine(videoFolder, video.VideoFileName);
                    if (System.IO.File.Exists(videoPath))
                    {
                        try
                        {
                            System.IO.File.Delete(videoPath);
                            _logger.LogInformation("Deleted physical raw mp4 master file: {Path}", videoPath);
                        }
                        catch (Exception fEx) { _logger.LogError(fEx, "Disk subsystem locked. Failed to erase physical video target: {Path}", videoPath); }
                    }

                    // 🎬 پاکسازی فایل‌های HLS مرتبط
                    var videoGuid = Path.GetFileNameWithoutExtension(video.VideoFileName);
                    var hlsDir = _hlsService.GetHlsDirectory(video.CourseId, videoGuid);
                    if (Directory.Exists(hlsDir))
                    {
                        try
                        {
                            Directory.Delete(hlsDir, recursive: true);
                            _logger.LogInformation("Purged correlated HLS output compilation folder tree: {Folder}", hlsDir);
                        }
                        catch (Exception dEx) { _logger.LogError(dEx, "Recursive storage sweep malfunction. Failed to clear HLS folder: {Folder}", hlsDir); }
                    }
                }

                var courseId = video.CourseId;
                _context.CourseVideos.Remove(video);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Video ID {VideoId} structural metadata wiped clean from database context.", videoId);

                await UpdateCourseDurationAsync(courseId);

                return Json(new { success = true, message = "Video deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected runtime breakdown processing removal configuration for Video ID: {VideoId}", videoId);
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpPost]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> ToggleActive([FromBody] IdRequest? payload, int? id)
        {
            var videoId = payload?.Id > 0 ? payload!.Id : (id ?? 0);
            _logger.LogInformation("Request to flip Active state received for Video ID: {VideoId}", videoId);

            try
            {
                if (videoId <= 0)
                    return Json(new { success = false, message = "Invalid video id" });

                var video = await _context.CourseVideos.FirstOrDefaultAsync(v => v.VideoId == videoId);
                if (video == null)
                {
                    _logger.LogWarning("ToggleActive failed. No video mapping found for ID: {VideoId}", videoId);
                    return Json(new { success = false, message = "Video not found" });
                }

                video.IsActive = !video.IsActive;
                video.LastUpdate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Video ID {VideoId} visibility flipped to Active={State}", videoId, video.IsActive);

                await UpdateCourseDurationAsync(video.CourseId);

                return Json(new { success = true, isActive = video.IsActive, message = video.IsActive ? "Video activated" : "Video deactivated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ToggleActive operation failed for Video ID: {VideoId}", videoId);
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpPost]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> ToggleFree([FromBody] IdRequest? payload, int? id)
        {
            var videoId = payload?.Id > 0 ? payload!.Id : (id ?? 0);
            _logger.LogInformation("Request to flip Free/Preview access flag for Video ID: {VideoId}", videoId);

            try
            {
                if (videoId <= 0)
                    return Json(new { success = false, message = "Invalid video id" });

                var video = await _context.CourseVideos.FirstOrDefaultAsync(v => v.VideoId == videoId);
                if (video == null)
                {
                    _logger.LogWarning("ToggleFree operation failed. Video context was missing for ID: {VideoId}", videoId);
                    return Json(new { success = false, message = "Video not found" });
                }

                video.IsFree = !video.IsFree;
                video.LastUpdate = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Video ID {VideoId} security layer toggled to FreePreview={State}", videoId, video.IsFree);

                return Json(new { success = true, isFree = video.IsFree, message = video.IsFree ? "Video marked free" : "Video marked paid" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ToggleFree transaction faulted for Video ID: {VideoId}", videoId);
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [Permission("Courses.Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            _logger.LogInformation("Loading entry configurations to build editor surface for Video ID: {VideoId}", id);

            var video = await _context.CourseVideos
                .Include(v => v.Course)
                .FirstOrDefaultAsync(v => v.VideoId == id);

            if (video == null)
            {
                _logger.LogWarning("Editor view model structure mapping failed. Video ID {VideoId} was completely absent.", id);
                TempData["ErrorMessage"] = "Video not found";
                return RedirectToAction("Index", "Courses");
            }

            ViewBag.CourseTitle = video.Course.Title;
            return View(video);
        }

        [HttpPost]
        [Permission("Courses.Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind(nameof(CourseVideo.VideoId), nameof(CourseVideo.CourseId), nameof(CourseVideo.Title), nameof(CourseVideo.Description), nameof(CourseVideo.DurationSeconds), nameof(CourseVideo.OrderIndex), nameof(CourseVideo.IsFree), nameof(CourseVideo.IsActive))] CourseVideo video, IFormFile? VideoFile, string? UploadedVideoToken)
        {
            _logger.LogInformation("Received structural update transaction for Video ID: {VideoId}. Course ID: {CourseId}", video.VideoId, video.CourseId);

            video.Title = video.Title?.Trim();
            video.Description = video.Description?.Trim();

            ModelState.Remove("Course");
            ModelState.Remove("VideoFileName");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("Video modifications aborted via Model Validation failure on Video ID: {VideoId}. Errors: {Errors}", video.VideoId, errors);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    TempData["ErrorMessage"] = errors;
                }

                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                if (course != null)
                {
                    ViewBag.CourseTitle = course.Title;
                }
                return View(video);
            }

            var existingVideo = await _context.CourseVideos.FirstOrDefaultAsync(v => v.VideoId == video.VideoId);
            if (existingVideo == null)
            {
                _logger.LogWarning("Update routine failed. Target record matching Video ID {VideoId} vanished from persistence storage tier.", video.VideoId);
                TempData["ErrorMessage"] = "Video not found";
                return RedirectToAction("Details", "Courses", new { id = video.CourseId });
            }

            bool fileProcessedViaToken = false;
            var directoryPath = $"uploads/courses/{video.CourseId}/videos";

            // 1. اگر فایل از قبل به صورت AJAX آپلود شده است، از توکن استفاده کن
            if (!string.IsNullOrWhiteSpace(UploadedVideoToken))
            {
                var sessionKey = TempUploadSessionPrefix + UploadedVideoToken;
                var uploadedFileName = HttpContext.Session.GetString(sessionKey);
                if (!string.IsNullOrWhiteSpace(uploadedFileName))
                {
                    _logger.LogInformation("Replacement video payload located via Session Token container configuration. Processing override sequence.");

                    // حذف ویدیو قدیمی در صورت وجود از طریق StorageService
                    if (!string.IsNullOrEmpty(existingVideo.VideoFileName))
                    {
                        try
                        {
                            await _storageService.DeleteFileAsync(directoryPath, existingVideo.VideoFileName);
                            _logger.LogInformation("Purged old master file stream via StorageService.");
                        }
                        catch (Exception fEx) { _logger.LogError(fEx, "Failed to strip file stack during edit cleanup."); }
                    }

                    existingVideo.VideoFileName = uploadedFileName;
                    HttpContext.Session.Remove(sessionKey);
                    fileProcessedViaToken = true;
                }
            }

            // 2. تنها در صورتی فایل مستقیم IFormFile را پردازش کن که فایل از طریق توکن AJAX دریافت نشده باشد
            if (!fileProcessedViaToken && VideoFile != null && VideoFile.Length > 0)
            {
                _logger.LogInformation("Processing direct inline file upload substitution streams.");
                if (VideoFile.Length > _uploadLimits.CourseVideoMaxBytes)
                {
                    _logger.LogWarning("File size threshold block breached during inline upload edit context. Length: {Length}", VideoFile.Length);
                    TempData["ErrorMessage"] = "Video size exceeds the allowed limit";
                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                    if (course != null)
                    {
                        ViewBag.CourseTitle = course.Title;
                    }
                    return View(video);
                }

                var allowedExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" };
                var fileExtension = Path.GetExtension(VideoFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Inline file format adjustment aborted. Unsupported type signature: {Extension}", fileExtension);
                    TempData["ErrorMessage"] = "Only video files are allowed";
                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                    if (course != null)
                    {
                        ViewBag.CourseTitle = course.Title;
                    }
                    return View(video);
                }

                // حذف ویدیو قدیمی از طریق StorageService
                if (!string.IsNullOrEmpty(existingVideo.VideoFileName))
                {
                    try
                    {
                        await _storageService.DeleteFileAsync(directoryPath, existingVideo.VideoFileName);
                    }
                    catch (Exception fEx) { _logger.LogError(fEx, "Failure to release file during synchronous override via StorageService."); }
                }

                var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

                try
                {
                    // آپلود مستقیم روی سرویس جدید
                    await _storageService.UploadFileAsync(VideoFile, directoryPath, uniqueFileName);
                    existingVideo.VideoFileName = uniqueFileName;
                    _logger.LogInformation("Direct inline file exchange process successfully established to target: {FileName}", uniqueFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload new video via StorageService.");
                    TempData["ErrorMessage"] = "Server storage error replacing video stream.";
                    return View(video);
                }
            }

            // به‌روزرسانی سایر فیلدهای متنی و وضعیت‌ها
            existingVideo.Title = video.Title;
            existingVideo.Description = video.Description;
            existingVideo.DurationSeconds = video.DurationSeconds;
            existingVideo.OrderIndex = video.OrderIndex;
            existingVideo.IsFree = video.IsFree;
            existingVideo.IsActive = video.IsActive;
            existingVideo.LastUpdate = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Modification specifications recorded to persistence database core for Video ID: {VideoId}", video.VideoId);
                await UpdateCourseDurationAsync(video.CourseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to commit changed field specifications database record for Video ID: {VideoId}", video.VideoId);
                TempData["ErrorMessage"] = $"Failed to update video: {ex.Message}";
                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == video.CourseId);
                if (course != null)
                {
                    ViewBag.CourseTitle = course.Title;
                }
                return View(video);
            }

            TempData["SuccessMessage"] = "Video updated successfully";
            return RedirectToAction("Details", "Courses", new { Area = "Admin", id = video.CourseId });
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 🎬 HLS Processing Helpers
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void TriggerHlsProcessing(int videoId, int courseId, string videoFileName)
        {
            var videoGuid = Path.GetFileNameWithoutExtension(videoFileName);

            // مسیرهای استاندارد
            var directoryPath = $"uploads/courses/{courseId}/videos";

            // نکته: اگر متد _hlsService.GetHlsDirectory خروجی مسیر فیزیکی (Physical) می‌دهد، 
            // بهتر است آن را آپدیت کنید تا فقط یک مسیر مجازی برگرداند (مثل uploads/courses/1/hls/guid)
            var hlsDirectoryPath = $"uploads/courses/{courseId}/hls/{videoGuid}";

            _logger.LogInformation("Asynchronous HLS Worker thread preparing job deployment parameters. Video ID: {VideoId}", videoId);

            // ثبت وضعیت اولیه در progress store
            _hlsProgress.SetQueued(videoId);

            // به دام انداختن وابستگی‌ها برای استفاده درون ترد پس‌زمینه
            var hlsService = _hlsService;
            var hlsProgress = _hlsProgress;
            var logger = _logger;
            var storageService = _storageService; // اطمینان حاصل کنید که IStorageService در کنترلر اینجکت شده باشد

            _ = Task.Run(async () =>
            {
                logger.LogInformation("Background Task worker explicitly spawned for tracking HLS segments generation. Video ID: {VideoId}", videoId);
                var progress = new Progress<int>(pct => hlsProgress.Report(videoId, pct));

                try
                {
                    // ۱. پاکسازی HLS قدیمی قبل از شروع مجدد از طریق StorageService
                    try
                    {
                        await storageService.DeleteDirectoryAsync(hlsDirectoryPath);
                        logger.LogInformation("Pre-flight workspace cleaner: Successfully wiped historic HLS build tree targets: {Folder}", hlsDirectoryPath);
                    }
                    catch (Exception clEx)
                    {
                        logger.LogError(clEx, "Pre-flight workspace cleaner crashed. Unable to wipe: {Folder}", hlsDirectoryPath);
                    }

                    // ۲. دریافت مسیری که FFmpeg بتواند بخواند (مسیر فیزیکی برای لوکال، لینک امضاشده طولانی برای S3)
                    var inputPath = await storageService.GetFileUrlForProcessingAsync(directoryPath, videoFileName);

                    // ۳. اجرای عملیات تبدیل
                    var result = await hlsService.ProcessVideoAsync(courseId, inputPath, videoGuid, progress);

                    if (result.Success)
                    {
                        hlsProgress.SetCompleted(videoId);
                        logger.LogInformation("Background Task finalized successfully! HLS Compilation built for Video ID: {VideoId}", videoId);
                    }
                    else
                    {
                        hlsProgress.SetFailed(videoId, result.Error ?? "Unknown error");
                        logger.LogError("Background Task transcode failed reported inside runtime worker framework for VideoId {VideoId}: {Error}", videoId, result.Error);
                    }
                }
                catch (Exception ex)
                {
                    hlsProgress.SetFailed(videoId, ex.Message);
                    logger.LogError(ex, "Fatal Thread Panic Exception caught during background processing pipeline routines for VideoId {VideoId}", videoId);
                }
            });
        }

        [HttpPost]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> ProcessHls([FromBody] IdRequest? payload, int? id)
        {
            var videoId = payload?.Id > 0 ? payload!.Id : (id ?? 0);
            _logger.LogWarning("Manual re-processing request received to force build HLS streaming stack for Video ID: {VideoId}", videoId);

            if (videoId <= 0)
                return Json(new { success = false, message = "Invalid video id" });

            var video = await _context.CourseVideos.FirstOrDefaultAsync(v => v.VideoId == videoId);
            if (video == null)
            {
                _logger.LogWarning("Manual HLS request abandoned. Video record not found for ID: {VideoId}", videoId);
                return Json(new { success = false, message = "Video not found" });
            }

            if (string.IsNullOrEmpty(video.VideoFileName))
            {
                _logger.LogWarning("Manual HLS request aborted. Master video binary link is missing for ID: {VideoId}", videoId);
                return Json(new { success = false, message = "Video has no file" });
            }

            // فراخوانی متد اصلاح‌شده که بلافاصله خروجی می‌دهد و کار در بک‌گراند انجام می‌شود
            TriggerHlsProcessing(video.VideoId, video.CourseId, video.VideoFileName);

            return Json(new { success = true, videoId, message = "HLS encoding started in background." });
        }

        [HttpGet]
        [Permission("Courses.View")]
        public IActionResult GetConversionProgress(int videoId)
        {
            var job = _hlsProgress.Get(videoId);
            if (job == null)
            {
                _logger.LogTrace("Polling request for conversion progress evaluated. Tracking object missing for Video ID: {VideoId}", videoId);
                return Json(new { found = false });
            }

            return Json(new
            {
                found = true,
                status = job.Status.ToString().ToLowerInvariant(),
                percent = job.Percent,
                error = job.Error
            });
        }

        [Permission("Courses.View")]
        public async Task<IActionResult> Details(int id)
        {
            _logger.LogInformation("Querying specific item view parameters for Video ID: {VideoId}", id);

            var video = await _context.CourseVideos
                .Include(v => v.Course)
                .FirstOrDefaultAsync(v => v.VideoId == id);

            if (video == null)
            {
                _logger.LogWarning("Item view requested but missing for Video ID: {VideoId}", id);
                TempData["ErrorMessage"] = "Video not found";
                return RedirectToAction("Index", "Courses");
            }

            return View(video);
        }

        [Permission("Courses.View")]
        public async Task<IActionResult> StreamVideo(int id)
        {
            _logger.LogInformation("Direct master stream extraction pipeline connection demanded for Video ID: {VideoId} by Admin Surface User.", id);

            var video = await _context.CourseVideos.FirstOrDefaultAsync(v => v.VideoId == id);
            if (video == null)
            {
                _logger.LogWarning("Master direct stream failed. Map entry missing for ID: {VideoId}", id);
                return NotFound();
            }

            var videoPath = Path.Combine(_environment.WebRootPath, "uploads", "courses", video.CourseId.ToString(), "videos", video.VideoFileName);

            if (!System.IO.File.Exists(videoPath))
            {
                _logger.LogError("Master direct stream failed. File vanished physically from infrastructure storage array. Target Path: {Path}", videoPath);
                return NotFound();
            }

            _logger.LogInformation("Instantiating multi-chunk range processing binary stream for direct feedback delivery pipeline on Video ID: {VideoId}", id);
            var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
            return File(fileStream, "video/mp4", enableRangeProcessing: true);
        }
    }
}