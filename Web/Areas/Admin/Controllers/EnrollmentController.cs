using Web.Extensions;
using Infrastructure.Persistence;
using Domain.Entities.Course;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Areas.Admin.ViewModels;
using Web.Attributes;

namespace Web.Areas.Admin.Controllers
{
    public class EnrollmentController : AdminBaseController
    {
        private readonly WebContext _context;
        private readonly ILogger<EnrollmentController> _logger;

        private const int DefaultPageSize = 25;

        public EnrollmentController(WebContext context, ILogger<EnrollmentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── Index ────────────────────────────────────────────────────────────

        [Permission("Courses.View")]
        public async Task<IActionResult> Index(
            string? searchQuery, int? courseId, bool? isCompleted, bool? isPaid,
            string sortBy = "date_desc", int page = 1)
        {
            var adminId = User.GetUserId();
            _logger.LogInformation("Admin {AdminId} viewing enrollment list. Filter: query='{Q}' course={C}", adminId, searchQuery, courseId);

            var filter = new EnrollmentFilterViewModel
            {
                SearchQuery = searchQuery,
                CourseId = courseId,
                IsCompleted = isCompleted,
                IsPaid = isPaid,
                SortBy = sortBy
            };

            var query = _context.CourseEnrollments
                .Include(e => e.User)
                .Include(e => e.Course)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();
                query = query.Where(e =>
                    (e.User.FirstName != null && e.User.FirstName.ToLower().Contains(q)) ||
                    (e.User.LastName != null && e.User.LastName.ToLower().Contains(q)) ||
                    (e.User.Email != null && e.User.Email.ToLower().Contains(q)) ||
                    e.User.UserName.ToLower().Contains(q) ||
                    e.Course.Title.ToLower().Contains(q));
            }

            if (courseId.HasValue)
                query = query.Where(e => e.CourseId == courseId.Value);

            if (isCompleted.HasValue)
                query = query.Where(e => e.IsCompleted == isCompleted.Value);

            if (isPaid.HasValue)
                query = query.Where(e => e.IsPaid == isPaid.Value);

            query = sortBy switch
            {
                "date_asc"     => query.OrderBy(e => e.EnrollmentDate),
                "progress_desc" => query.OrderByDescending(e => e.ProgressPercentage),
                "progress_asc"  => query.OrderBy(e => e.ProgressPercentage),
                "name_asc"     => query.OrderBy(e => e.User.FirstName).ThenBy(e => e.User.LastName),
                "course_asc"   => query.OrderBy(e => e.Course.Title),
                _               => query.OrderByDescending(e => e.EnrollmentDate)
            };

            var totalCount = await query.CountAsync();
            var pageSize = DefaultPageSize;
            var skip = (page - 1) * pageSize;

            var rows = await query
                .Skip(skip)
                .Take(pageSize)
                .Select(e => new EnrollmentRowViewModel
                {
                    EnrollmentId      = e.EnrollmentId,
                    UserId            = e.UserId,
                    UserFullName      = (e.User.FirstName ?? "") + " " + (e.User.LastName ?? ""),
                    UserName          = e.User.UserName,
                    UserEmail         = e.User.Email,
                    CourseId          = e.CourseId,
                    CourseTitle       = e.Course.Title,
                    CourseIsActive    = e.Course.IsActive,
                    EnrollmentDate    = e.EnrollmentDate,
                    ProgressPercentage = e.ProgressPercentage,
                    IsCompleted       = e.IsCompleted,
                    CompletionDate    = e.CompletionDate,
                    IsPaid            = e.IsPaid,
                    PaymentAmount     = e.PaymentAmount
                })
                .ToListAsync();

            var courses = await _context.Courses
                .OrderBy(c => c.Title)
                .Select(c => new CourseSelectItem
                {
                    CourseId        = c.CourseId,
                    Title           = c.Title,
                    EnrollmentCount = c.Enrollments.Count
                })
                .ToListAsync();

            var vm = new EnrollmentIndexViewModel
            {
                Enrollments = rows,
                Filter      = filter,
                TotalCount  = totalCount,
                Page        = page,
                PageSize    = pageSize,
                Courses     = courses
            };

            return View(vm);
        }

        // ── Add Enrollment (AJAX) ────────────────────────────────────────────

        [HttpPost]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> AddEnrollment([FromBody] AddEnrollmentRequest? request)
        {
            var adminId = User.GetUserId();

            if (request == null || request.UserId <= 0 || request.CourseId <= 0)
                return Json(new { success = false, message = "Invalid request." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId && !u.IsDelete);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == request.CourseId);
            if (course == null)
                return Json(new { success = false, message = "Course not found." });

            var existing = await _context.CourseEnrollments
                .FirstOrDefaultAsync(e => e.UserId == request.UserId && e.CourseId == request.CourseId);
            if (existing != null)
                return Json(new { success = false, message = "User is already enrolled in this course." });

            var enrollment = new CourseEnrollment
            {
                UserId         = request.UserId,
                CourseId       = request.CourseId,
                EnrollmentDate = DateTime.UtcNow,
                IsPaid         = request.IsPaid,
                PaymentAmount  = request.IsPaid ? course.Price : 0,
                PaymentDate    = request.IsPaid ? DateTime.UtcNow : null,
                ProgressPercentage = 0,
                IsCompleted    = false
            };

            _context.CourseEnrollments.Add(enrollment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("AUDIT: Admin {AdminId} enrolled User {UserId} in Course {CourseId} (paid={Paid}).",
                adminId, request.UserId, request.CourseId, request.IsPaid);

            return Json(new { success = true, message = "User enrolled successfully.", enrollmentId = enrollment.EnrollmentId });
        }

        // ── Remove Enrollment (AJAX) ─────────────────────────────────────────

        [HttpPost]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> RemoveEnrollment([FromBody] RemoveEnrollmentRequest? request)
        {
            var adminId = User.GetUserId();

            if (request == null || request.EnrollmentId <= 0)
                return Json(new { success = false, message = "Invalid request." });

            var enrollment = await _context.CourseEnrollments
                .Include(e => e.User)
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.EnrollmentId == request.EnrollmentId);

            if (enrollment == null)
                return Json(new { success = false, message = "Enrollment not found." });

            // Cascade-delete video progress for this user + course
            var progressRecords = await _context.VideoProgresses
                .Where(p => p.UserId == enrollment.UserId && p.Video.CourseId == enrollment.CourseId)
                .ToListAsync();

            if (progressRecords.Any())
                _context.VideoProgresses.RemoveRange(progressRecords);

            _context.CourseEnrollments.Remove(enrollment);
            await _context.SaveChangesAsync();

            _logger.LogWarning("AUDIT: Admin {AdminId} removed User {UserId} ({UserName}) from Course {CourseId} ({CourseTitle}). Progress records deleted: {ProgressCount}.",
                adminId, enrollment.UserId, enrollment.User?.UserName,
                enrollment.CourseId, enrollment.Course?.Title, progressRecords.Count);

            return Json(new { success = true, message = "Enrollment removed successfully." });
        }

        // ── Bulk Revoke (AJAX) ───────────────────────────────────────────────

        [HttpPost]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> BulkRevoke([FromBody] BulkRevokeRequest? request)
        {
            var adminId = User.GetUserId();

            if (request == null || request.CourseId <= 0)
                return Json(new { success = false, message = "Invalid request." });

            var course = await _context.Courses.FindAsync(request.CourseId);
            if (course == null)
                return Json(new { success = false, message = "Course not found." });

            var enrollments = await _context.CourseEnrollments
                .Where(e => e.CourseId == request.CourseId)
                .ToListAsync();

            if (!enrollments.Any())
                return Json(new { success = false, message = "No enrollments found for this course." });

            // Delete all video progress for these users in this course
            var userIds = enrollments.Select(e => e.UserId).ToList();
            var progressRecords = await _context.VideoProgresses
                .Where(p => userIds.Contains(p.UserId) && p.Video.CourseId == request.CourseId)
                .ToListAsync();

            if (progressRecords.Any())
                _context.VideoProgresses.RemoveRange(progressRecords);

            _context.CourseEnrollments.RemoveRange(enrollments);
            await _context.SaveChangesAsync();

            _logger.LogWarning("AUDIT: Admin {AdminId} bulk-revoked all {Count} enrollments from Course {CourseId} ({CourseTitle}).",
                adminId, enrollments.Count, request.CourseId, course.Title);

            return Json(new { success = true, message = $"Removed {enrollments.Count} enrollment(s) from '{course.Title}'.", count = enrollments.Count });
        }

        // ── Bulk Transfer (GET + POST) ────────────────────────────────────────

        [Permission("Courses.Edit")]
        public async Task<IActionResult> BulkTransfer()
        {
            var courses = await _context.Courses
                .OrderBy(c => c.Title)
                .Select(c => new CourseSelectItem
                {
                    CourseId        = c.CourseId,
                    Title           = c.Title,
                    EnrollmentCount = c.Enrollments.Count
                })
                .ToListAsync();

            return View(new BulkTransferViewModel { AvailableCourses = courses });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permission("Courses.Edit")]
        public async Task<IActionResult> BulkTransfer(BulkTransferViewModel model)
        {
            var adminId = User.GetUserId();

            // Reload courses for the form regardless of outcome
            model.AvailableCourses = await _context.Courses
                .OrderBy(c => c.Title)
                .Select(c => new CourseSelectItem
                {
                    CourseId        = c.CourseId,
                    Title           = c.Title,
                    EnrollmentCount = c.Enrollments.Count
                })
                .ToListAsync();

            if (!ModelState.IsValid)
                return View(model);

            if (model.FromCourseId == model.ToCourseId)
            {
                ModelState.AddModelError(string.Empty, "Source and destination courses must be different.");
                return View(model);
            }

            var fromCourse = await _context.Courses.FindAsync(model.FromCourseId!.Value);
            var toCourse   = await _context.Courses.FindAsync(model.ToCourseId!.Value);

            if (fromCourse == null || toCourse == null)
            {
                ModelState.AddModelError(string.Empty, "One or both courses were not found.");
                return View(model);
            }

            var result = new BulkTransferResult();

            var sourceEnrollments = await _context.CourseEnrollments
                .Where(e => e.CourseId == model.FromCourseId.Value)
                .ToListAsync();

            if (!sourceEnrollments.Any())
            {
                ModelState.AddModelError(string.Empty, $"No enrollments found in '{fromCourse.Title}'.");
                return View(model);
            }

            var existingDestUserIds = await _context.CourseEnrollments
                .Where(e => e.CourseId == model.ToCourseId.Value)
                .Select(e => e.UserId)
                .ToHashSetAsync();

            var newEnrollments = new List<CourseEnrollment>();
            var toRemove       = new List<CourseEnrollment>();

            foreach (var source in sourceEnrollments)
            {
                bool alreadyInDest = existingDestUserIds.Contains(source.UserId);

                if (alreadyInDest && !model.OverrideExisting)
                {
                    result.Skipped++;
                    continue;
                }

                if (alreadyInDest && model.OverrideExisting)
                {
                    // Update the existing destination enrollment progress
                    var destEnrollment = await _context.CourseEnrollments
                        .FirstOrDefaultAsync(e => e.UserId == source.UserId && e.CourseId == model.ToCourseId.Value);
                    if (destEnrollment != null)
                    {
                        destEnrollment.ProgressPercentage = source.ProgressPercentage;
                        destEnrollment.IsCompleted        = source.IsCompleted;
                        destEnrollment.CompletionDate     = source.CompletionDate;
                        if (model.CopyPaymentInfo)
                        {
                            destEnrollment.IsPaid        = source.IsPaid;
                            destEnrollment.PaymentAmount = source.PaymentAmount;
                            destEnrollment.PaymentDate   = source.PaymentDate;
                        }
                    }
                }
                else
                {
                    newEnrollments.Add(new CourseEnrollment
                    {
                        UserId             = source.UserId,
                        CourseId           = model.ToCourseId.Value,
                        EnrollmentDate     = DateTime.UtcNow,
                        ProgressPercentage = 0,
                        IsCompleted        = false,
                        IsPaid             = model.CopyPaymentInfo ? source.IsPaid : false,
                        PaymentAmount      = model.CopyPaymentInfo ? source.PaymentAmount : 0,
                        PaymentDate        = model.CopyPaymentInfo ? source.PaymentDate : null
                    });
                }

                result.Transferred++;

                if (model.RemoveFromSource)
                    toRemove.Add(source);
            }

            if (newEnrollments.Any())
                await _context.CourseEnrollments.AddRangeAsync(newEnrollments);

            if (toRemove.Any())
            {
                // Also remove progress records from source course for removed enrollments
                var removedUserIds = toRemove.Select(e => e.UserId).ToList();
                var progressToDelete = await _context.VideoProgresses
                    .Where(p => removedUserIds.Contains(p.UserId) && p.Video.CourseId == model.FromCourseId.Value)
                    .ToListAsync();
                if (progressToDelete.Any())
                    _context.VideoProgresses.RemoveRange(progressToDelete);

                _context.CourseEnrollments.RemoveRange(toRemove);
                result.Removed = toRemove.Count;
            }

            await _context.SaveChangesAsync();

            _logger.LogWarning("AUDIT: Admin {AdminId} bulk-transferred enrollments from Course {FromId} to Course {ToId}. Transferred={T}, Skipped={S}, Removed={R}.",
                adminId, model.FromCourseId, model.ToCourseId, result.Transferred, result.Skipped, result.Removed);

            model.Result = result;
            return View(model);
        }

        // ── AJAX Search Helpers ──────────────────────────────────────────────

        [HttpGet]
        [Permission("Courses.View")]
        public async Task<IActionResult> SearchUsers(string? q, int? excludeCourseId)
        {
            var query = _context.Users
                .Where(u => !u.IsDelete)
                .AsQueryable();

            if (excludeCourseId.HasValue)
            {
                var enrolledIds = await _context.CourseEnrollments
                    .Where(e => e.CourseId == excludeCourseId.Value)
                    .Select(e => e.UserId)
                    .ToListAsync();
                query = query.Where(u => !enrolledIds.Contains(u.UserId));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var lower = q.Trim().ToLower();
                query = query.Where(u =>
                    (u.FirstName != null && u.FirstName.ToLower().Contains(lower)) ||
                    (u.LastName  != null && u.LastName.ToLower().Contains(lower)) ||
                    (u.Email     != null && u.Email.ToLower().Contains(lower)) ||
                    u.UserName.ToLower().Contains(lower));
            }

            var users = await query
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .Take(20)
                .Select(u => new
                {
                    u.UserId,
                    FullName = (u.FirstName ?? "") + " " + (u.LastName ?? ""),
                    u.UserName,
                    u.Email
                })
                .ToListAsync();

            return Json(users);
        }

        [HttpGet]
        [Permission("Courses.View")]
        public async Task<IActionResult> SearchCourses(string? q, int? excludeUserId)
        {
            var query = _context.Courses.AsQueryable();

            if (excludeUserId.HasValue)
            {
                var enrolledCourseIds = await _context.CourseEnrollments
                    .Where(e => e.UserId == excludeUserId.Value)
                    .Select(e => e.CourseId)
                    .ToListAsync();
                query = query.Where(c => !enrolledCourseIds.Contains(c.CourseId));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var lower = q.Trim().ToLower();
                query = query.Where(c => c.Title.ToLower().Contains(lower));
            }

            var courses = await query
                .OrderBy(c => c.Title)
                .Take(20)
                .Select(c => new
                {
                    c.CourseId,
                    c.Title,
                    c.IsActive,
                    Price = c.Price.ToString("N0"),
                    EnrollmentCount = c.Enrollments.Count
                })
                .ToListAsync();

            return Json(courses);
        }
    }
}
