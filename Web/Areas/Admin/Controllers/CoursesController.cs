using Domain.Configuration;
using Web.Extensions; // Added for GetUserId consistent with previous implementations
using Infrastructure.Persistence;
using Domain.Entities.Course;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Application.Interfaces;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class CoursesController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly UploadLimitsOptions _uploadLimits;
        private readonly IHlsProcessingService _hlsService;
        private readonly ILogger<CoursesController> _logger;
        private readonly IStorageService _storageService;

        public class IdRequest
        {
            public int Id { get; set; }
        }

        public CoursesController(
            WebContext context,
            IWebHostEnvironment environment,
            IOptions<UploadLimitsOptions> uploadLimits,
            IHlsProcessingService hlsService,
            ILogger<CoursesController> logger, IStorageService storageService)
        {
            _context = context;
            _environment = environment;
            _uploadLimits = uploadLimits.Value;
            _hlsService = hlsService;
            _logger = logger;
            _storageService = storageService;
        }

        [Permission("Courses.View")]
        public async Task<IActionResult> Index()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("Admin user {UserId} requested the management course list.", currentUserId);

            try
            {
                var courses = await _context.Courses
                    .Include(c => c.Videos)
                    .OrderByDescending(c => c.CreateDate)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} courses for management index.", courses.Count);
                return View(courses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred while fetching the course list for User {UserId}.", currentUserId);
                throw;
            }
        }

        [Permission("Courses.Create")]
        public IActionResult Create()
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} is navigating to the course creation page.", currentUserId);
            return View();
        }

        [Permission("Courses.Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} requested the edit view for Course ID: {CourseId}", currentUserId, id);

            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == id);
            if (course == null)
            {
                _logger.LogWarning("Edit view request failed. Course ID {CourseId} was not found. Requested by User {UserId}.", id, currentUserId);
                TempData["ErrorMessage"] = "Course not found";
                return RedirectToAction(nameof(Index));
            }

            var imagePath = Path.Combine(_environment.WebRootPath, "uploads", "courses", course.CourseId.ToString(), course.ImageName);
            bool imageExists = System.IO.File.Exists(imagePath);
            ViewBag.ImageExists = imageExists;

            // FIX: Passing strongly-typed local variable 'imageExists' instead of dynamic 'ViewBag.ImageExists' to solve compiler error.
            _logger.LogInformation("Course ID {CourseId} found. Cover image local existence status: {ImageExists}", id, imageExists);

            return View(course);
        }

        [Permission("Courses.View")]
        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} requested extensive details for Course ID: {CourseId}", currentUserId, id);

            var course = await _context.Courses
                 .Include(c => c.Videos.OrderBy(v => v.OrderIndex))
                 .Include(c => c.Pdfs.OrderBy(p => p.CreateDate))
                 .Include(c => c.Enrollments)
                     .ThenInclude(e => e.User)
                 .FirstOrDefaultAsync(c => c.CourseId == id);

            if (course == null)
            {
                _logger.LogWarning("Details request failed. Course ID {CourseId} not found for User {UserId}.", id, currentUserId);
                TempData["ErrorMessage"] = "Course not found";
                return RedirectToAction(nameof(Index));
            }

            // مسیر پیش‌فرض دایرکتوری برای ویدیوهای این دوره
            var directoryPath = $"uploads/courses/{course.CourseId}/videos";

            // محاسبه حجم و وضعیت فایل هر ویدیو با استفاده از Storage Service
            var videoFileSizes = new Dictionary<int, long>();
            var videoFileExists = new Dictionary<int, bool>();

            if (course.Videos != null)
            {
                foreach (var video in course.Videos)
                {
                    if (!string.IsNullOrEmpty(video.VideoFileName))
                    {
                        // بررسی وجود فایل به صورت مستقل از سیستم‌عامل
                        bool exists = await _storageService.FileExistsAsync(directoryPath, video.VideoFileName);
                        videoFileExists[video.VideoId] = exists;

                        // دریافت سایز فایل
                        videoFileSizes[video.VideoId] = exists ? await _storageService.GetFileSizeAsync(directoryPath, video.VideoFileName) : 0;
                    }
                    else
                    {
                        videoFileExists[video.VideoId] = false;
                        videoFileSizes[video.VideoId] = 0;
                    }
                }
            }

            ViewBag.VideoFileSizes = videoFileSizes;
            ViewBag.VideoFileExists = videoFileExists;

            // وضعیت HLS هر ویدیو
            var hlsExists = new Dictionary<int, bool>();
            if (course.Videos != null)
            {
                foreach (var video in course.Videos)
                {
                    if (!string.IsNullOrEmpty(video.VideoFileName))
                    {
                        var videoGuid = Path.GetFileNameWithoutExtension(video.VideoFileName);
                        // توجه: اگر HLS هم به باکت منتقل می‌شود، سرویس HLS نیز در آینده باید آپدیت شود
                        hlsExists[video.VideoId] = await _hlsService.HlsExistsAsync(video.CourseId, videoGuid);
                    }
                    else
                    {
                        hlsExists[video.VideoId] = false;
                    }
                }
            }
            ViewBag.HlsExists = hlsExists;

            // بارگذاری تمام کاربران برای modal ثبت‌نام دانشجو
            var allUsers = await _context.Users
                .Where(u => !u.IsDelete)
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Select(u => new { u.UserId, u.FirstName, u.LastName, u.Email, u.UserName })
                .ToListAsync();
            ViewBag.AllUsers = allUsers;

            _logger.LogInformation("Successfully compiled metadata for Course ID: {CourseId}. Videos: {VideoCount}, PDFs: {PdfCount}, Enrollments: {EnrollmentCount}. Action completed by User {UserId}.",
                id, course.Videos?.Count ?? 0, course.Pdfs?.Count ?? 0, course.Enrollments?.Count ?? 0, currentUserId);

            return View(course);
        }
        [HttpPost]
        [Permission("Courses.Delete")]
        public async Task<IActionResult> Delete([FromBody] IdRequest? payload, int? id)
        {
            var currentUserId = User.GetUserId();
            var courseId = payload?.Id > 0 ? payload!.Id : (id ?? 0);

            _logger.LogWarning("AUDIT: User {UserId} initiated a DELETE request for Course ID: {CourseId}", currentUserId, courseId);

            if (courseId <= 0)
            {
                _logger.LogWarning("Delete aborted. Invalid Course ID structure passed: {CourseId} by User {UserId}", courseId, currentUserId);
                return Json(new { success = false, message = "Invalid course id" });
            }

            try
            {
                var course = await _context.Courses
                    .Include(c => c.Videos)
                    .Include(c => c.Enrollments)
                    .FirstOrDefaultAsync(c => c.CourseId == courseId);

                if (course == null)
                {
                    _logger.LogWarning("Delete failed. Course ID {CourseId} does not exist in the database. Action by User {UserId}.", courseId, currentUserId);
                    return Json(new { success = false, message = "Course not found" });
                }

                if (course.Enrollments.Any())
                {
                    _logger.LogWarning("Delete rejected. Course '{CourseTitle}' (ID: {CourseId}) has {Count} active student enrollment(s). Action blocked for User {UserId}.", course.Title, courseId, course.Enrollments.Count, currentUserId);
                    return Json(new { success = false, message = "Course has enrolled students and cannot be deleted" });
                }

                // حذف تصویر دوره
                if (!string.IsNullOrEmpty(course.ImageName) && course.ImageName != "default.jpg")
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "courses");
                    var imagePath = Path.Combine(uploadsFolder, course.ImageName);
                    if (System.IO.File.Exists(imagePath))
                    {
                        try
                        {
                            System.IO.File.Delete(imagePath);
                            _logger.LogInformation("Successfully purged physical cover image: '{ImageName}' for Course ID: {CourseId}", course.ImageName, courseId);
                        }
                        catch (Exception imgEx)
                        {
                            _logger.LogError(imgEx, "Failed to delete physical cover image file on disk: {ImagePath}", imagePath);
                        }
                    }
                }

                // حذف پوشه ویدیوهای دوره
                var videosFolder = Path.Combine(_environment.WebRootPath, "uploads", "courses", courseId.ToString(), "videos");
                if (Directory.Exists(videosFolder))
                {
                    try
                    {
                        Directory.Delete(videosFolder, true);
                        _logger.LogInformation("Successfully deleted videos directory recursively from storage: {VideosFolder}", videosFolder);
                    }
                    catch (Exception dirEx)
                    {
                        _logger.LogError(dirEx, "Failed to delete physical videos folder recursively: {VideosFolder}", videosFolder);
                    }
                }

                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();

                _logger.LogInformation("AUDIT SUCCESS: Course '{CourseTitle}' (ID: {CourseId}) and all metadata records permanently removed by User {UserId}.", course.Title, courseId, currentUserId);
                return Json(new { success = true, message = "Course deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error occurred while deleting Course ID: {CourseId} by User {UserId}", courseId, currentUserId);
                return Json(new { success = false, message = "Delete failed internally" });
            }
        }

        [HttpPost]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> ToggleActive([FromBody] IdRequest? payload, int? id)
        {
            var currentUserId = User.GetUserId();
            var courseId = payload?.Id > 0 ? payload!.Id : (id ?? 0);

            _logger.LogInformation("User {UserId} requested ToggleActive state change for Course ID: {CourseId}", currentUserId, courseId);

            if (courseId <= 0)
            {
                _logger.LogWarning("ToggleActive aborted due to invalid Course ID context: {CourseId}", courseId);
                return Json(new { success = false, message = "Invalid course id" });
            }

            try
            {
                var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId);
                if (course == null)
                {
                    _logger.LogWarning("ToggleActive failed. Course ID {CourseId} not found in database. Requested by User {UserId}.", courseId, currentUserId);
                    return Json(new { success = false, message = "Course not found" });
                }

                course.IsActive = !course.IsActive;
                course.LastUpdate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("AUDIT: Course ID {CourseId} active status modified to {IsActive} by User {UserId}.", courseId, course.IsActive, currentUserId);
                return Json(new { success = true, isActive = course.IsActive, message = course.IsActive ? "Course activated" : "Course deactivated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during ToggleActive execution for Course ID: {CourseId} by User {UserId}", courseId, currentUserId);
                return Json(new { success = false, message = "An internal error occurred." });
            }
        }

        [HttpPost]
        [Permission("Courses.Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(nameof(Course.Title), nameof(Course.Description), nameof(Course.Price), nameof(Course.IsActive))] Course course, IFormFile? ImageFile)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} is attempting to create a new course titled: '{Title}'.", currentUserId, course?.Title);

            if (course == null) return BadRequest();

            course.Title = course.Title?.Trim();
            course.Description = course.Description?.Trim();

            ModelState.Remove("ImageName");
            ModelState.Remove("Videos");
            ModelState.Remove("Pdfs");
            ModelState.Remove("Enrollments");
            ModelState.Remove("CreateDate");
            ModelState.Remove("LastUpdate");
            ModelState.Remove("DurationHours");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("Course creation rejected due to validation errors. User {UserId}. Errors: {Errors}", currentUserId, errors);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    TempData["ErrorMessage"] = errors;
                }
                return View(course);
            }

            if (ImageFile != null && ImageFile.Length > 0)
            {
                if (ImageFile.Length > _uploadLimits.CourseCoverMaxBytes)
                {
                    _logger.LogWarning("Course creation failed. Uploaded cover size {Length} bytes exceeds configured limit {Limit} bytes. User: {UserId}.", ImageFile.Length, _uploadLimits.CourseCoverMaxBytes, currentUserId);
                    TempData["ErrorMessage"] = "Cover image size exceeds the allowed limit";
                    return View(course);
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Course creation failed. File format '{Extension}' is unauthorized. User: {UserId}.", fileExtension, currentUserId);
                    TempData["ErrorMessage"] = "Only image files are allowed";
                    return View(course);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "courses");
                try
                {
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    course.ImageName = uniqueFileName;
                    _logger.LogInformation("Course cover image safely processed and saved on disk as: {UniqueName}", uniqueFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IO exception occurred while executing file stream write for new course cover image. Admin User: {UserId}.", currentUserId);
                    TempData["ErrorMessage"] = "Server error processing cover image.";
                    return View(course);
                }
            }
            else
            {
                course.ImageName = "default.jpg";
                _logger.LogInformation("No user-defined cover uploaded. Defaulting cover art to 'default.jpg' for course '{Title}'.", course.Title);
            }

            course.CreateDate = DateTime.UtcNow;
            course.DurationHours = 0;

            try
            {
                _context.Courses.Add(course);
                await _context.SaveChangesAsync();
                _logger.LogInformation("AUDIT SUCCESS: New course entry '{Title}' created successfully. Assigned DB Key: {CourseId} by Admin {UserId}.", course.Title, course.CourseId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database transaction failed while appending new course records for title '{Title}'.", course.Title);
                TempData["ErrorMessage"] = $"Failed to save course: {ex.Message}";
                return View(course);
            }

            TempData["SuccessMessage"] = "Course created successfully";
            return RedirectToAction(nameof(Details), new { area = "Admin", id = course.CourseId });
        }

        [HttpPost]
        [Permission("Courses.Edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind(nameof(Course.CourseId), nameof(Course.Title), nameof(Course.Description), nameof(Course.Price), nameof(Course.IsActive))] Course course, IFormFile? ImageFile)
        {
            var currentUserId = User.GetUserId();
            _logger.LogInformation("User {UserId} is trying to commit edits for Course ID: {CourseId}.", currentUserId, course?.CourseId);

            if (course == null) return BadRequest();

            course.Title = course.Title?.Trim();
            course.Description = course.Description?.Trim();

            ModelState.Remove("Videos");
            ModelState.Remove("Pdfs");
            ModelState.Remove("Enrollments");
            ModelState.Remove("DurationHours");

            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                _logger.LogWarning("Course edit validation aborted. Course ID: {CourseId}. User: {UserId}. Errors: {Errors}", course.CourseId, currentUserId, errors);
                if (!string.IsNullOrWhiteSpace(errors))
                {
                    TempData["ErrorMessage"] = errors;
                }
                return View(course);
            }

            var existingCourse = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == course.CourseId);
            if (existingCourse == null)
            {
                _logger.LogWarning("Edit commit failed. Target Course ID {CourseId} was not found in the persistent store. Action by User {UserId}.", course.CourseId, currentUserId);
                TempData["ErrorMessage"] = "Course not found";
                return RedirectToAction(nameof(Index));
            }

            var checkImgFolder = Path.Combine(_environment.WebRootPath, "uploads", "courses", existingCourse.CourseId.ToString(), existingCourse.ImageName);
            bool localImageExists = System.IO.File.Exists(checkImgFolder);
            ViewBag.ImageExists = localImageExists;

            if (!localImageExists && ImageFile == null)
            {
                _logger.LogWarning("Validation failed. Asset metadata mismatch: local cover artwork asset is completely missing and no replacement payload was uploaded for Course ID: {CourseId}", course.CourseId);
                ModelState.AddModelError("ImageName", "Course image not found");
                return View(course);
            }

            if (ImageFile != null && ImageFile.Length > 0)
            {
                if (ImageFile.Length > _uploadLimits.CourseCoverMaxBytes)
                {
                    _logger.LogWarning("Edit commit rejected. Replacement cover size {Length} bytes exceeds constraints for Course ID: {CourseId}. User: {UserId}.", ImageFile.Length, course.CourseId, currentUserId);
                    TempData["ErrorMessage"] = "Cover image size exceeds the allowed limit";
                    return View(course);
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(ImageFile.FileName).ToLower();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Edit commit rejected. File format structure '{Extension}' is explicitly blocked. Course ID: {CourseId}.", fileExtension, course.CourseId);
                    TempData["ErrorMessage"] = "Only image files are allowed";
                    return View(course);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "courses");
                try
                {
                    Directory.CreateDirectory(uploadsFolder);

                    // حذف تصویر قدیمی در صورت لزوم
                    if (!string.IsNullOrEmpty(existingCourse.ImageName) && existingCourse.ImageName != "default.jpg")
                    {
                        var oldImagePath = Path.Combine(uploadsFolder, existingCourse.ImageName);
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                            _logger.LogInformation("Purged archaic cover image file: '{OldImage}' from disk storage for Course ID: {CourseId}", existingCourse.ImageName, course.CourseId);
                        }
                    }

                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    existingCourse.ImageName = uniqueFileName;
                    _logger.LogInformation("Replaced cover file asset safely with identity name: '{UniqueName}' for Course ID: {CourseId}", uniqueFileName, course.CourseId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Storage interaction error occurred while substituting cover graphics payload on disk. Course ID: {CourseId}.", course.CourseId);
                    TempData["ErrorMessage"] = "Server error updating the cover image.";
                    return View(course);
                }
            }

            _logger.LogInformation("AUDIT: Updating data records for Course ID {CourseId}. Retargeting values -> Title: '{OldTitle}' to '{NewTitle}', Price: {OldPrice} to {NewPrice}.",
                existingCourse.CourseId, existingCourse.Title, course.Title, existingCourse.Price, course.Price);

            existingCourse.Title = course.Title;
            existingCourse.Description = course.Description;
            existingCourse.Price = course.Price;
            existingCourse.IsActive = course.IsActive;
            existingCourse.LastUpdate = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("AUDIT SUCCESS: Course entity context updated for Course ID: {CourseId} by Admin User {UserId}.", course.CourseId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data execution pipeline encountered an error during SaveChanges for Course ID: {CourseId}", course.CourseId);
                TempData["ErrorMessage"] = $"Failed to update course: {ex.Message}";
                return View(course);
            }

            TempData["SuccessMessage"] = "Course updated successfully";
            return RedirectToAction(nameof(Details), new { id = course.CourseId });
        }
    }
}