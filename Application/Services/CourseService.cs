using Application.Interfaces;
using Application.Interfaces;
using Domain.Entities.Course;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public class CourseService : ICourseService
    {
        private readonly IApplicationDbContext _context;

        public CourseService(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Course>> GetActiveCoursesAsync()
        {
            return await _context.Courses
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreateDate)
                .ToListAsync();
        }

        public async Task<List<Course>> GetUserEnrolledCoursesAsync(int userId)
        {
            return await _context.CourseEnrollments
                .Where(e => e.UserId == userId)
                .Include(e => e.Course)
                .Select(e => e.Course)
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreateDate)
                .ToListAsync();
        }

        public async Task<List<CourseEnrollment>> GetUserEnrollmentsAsync(int userId)
        {
            return await _context.CourseEnrollments
                .Include(e => e.Course)
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.EnrollmentDate)
                .ToListAsync();
        }

        public async Task<Course?> GetCourseByIdAsync(int courseId)
        {
            return await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseId == courseId && c.IsActive);
        }

        public async Task<Course?> GetCourseWithDetailsAsync(int courseId)
        {
            return await _context.Courses
                .Include(c => c.Videos.Where(v => v.IsActive))
                .Include(c => c.Pdfs.Where(p => p.IsActive))
                .FirstOrDefaultAsync(c => c.CourseId == courseId && c.IsActive);
        }

        public async Task<Dictionary<int, Domain.Entities.Course.VideoProgress>> GetVideoProgressDictAsync(int userId, int courseId)
        {
            return await _context.VideoProgresses
                .Where(p => p.UserId == userId && p.Video.CourseId == courseId)
                .ToDictionaryAsync(p => p.VideoId);
        }

        public async Task<bool> IsUserEnrolledAsync(int userId, int courseId)
        {
            return await _context.CourseEnrollments
                .AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
        }

        public async Task<CourseEnrollment?> GetUserEnrollmentAsync(int userId, int courseId)
        {
            return await _context.CourseEnrollments
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);
        }

        public async Task<bool> EnrollUserInCourseAsync(int userId, int courseId, decimal paymentAmount)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null || !course.IsActive)
                return false;

            var existingEnrollment = await GetUserEnrollmentAsync(userId, courseId);
            if (existingEnrollment != null)
                return false;

            var enrollment = new CourseEnrollment
            {
                UserId = userId,
                CourseId = courseId,
                EnrollmentDate = DateTime.UtcNow,
                IsPaid = paymentAmount >= course.Price,
                PaymentAmount = paymentAmount,
                PaymentDate = paymentAmount > 0 ? DateTime.UtcNow : null,
                ProgressPercentage = 0,
                IsCompleted = false
            };

            _context.CourseEnrollments.Add(enrollment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CanUserAccessVideoAsync(int userId, int videoId)
        {
            var video = await _context.CourseVideos
                .Include(v => v.Course)
                .FirstOrDefaultAsync(v => v.VideoId == videoId);

            if (video == null || !video.IsActive || !video.Course.IsActive)
                return false;

            // اگر ویدیو رایگان است
            if (video.IsFree)
                return true;

            // بررسی ثبت‌نام کاربر
            return await IsUserEnrolledAsync(userId, video.CourseId);
        }

        public async Task<bool> CanUserAccessPdfAsync(int userId, int pdfId)
        {
            var pdf = await _context.CoursePdfs
                .Include(p => p.Course)
                .FirstOrDefaultAsync(p => p.PdfId == pdfId);

            if (pdf == null || !pdf.IsActive || !pdf.Course.IsActive)
                return false;

            // اگر PDF رایگان است
            if (pdf.Price == 0)
                return true;

            // بررسی ثبت‌نام کاربر
            return await IsUserEnrolledAsync(userId, pdf.CourseId);
        }

        public async Task<CourseVideo?> GetVideoByIdAsync(int videoId)
        {
            return await _context.CourseVideos
                .Include(v => v.Course)
                .FirstOrDefaultAsync(v => v.VideoId == videoId && v.IsActive);
        }

        public async Task<CoursePdf?> GetPdfByIdAsync(int pdfId)
        {
            return await _context.CoursePdfs
                .Include(p => p.Course)
                .FirstOrDefaultAsync(p => p.PdfId == pdfId && p.IsActive);
        }

        public async Task<List<CourseVideo>> GetCourseVideosAsync(int courseId, int userId)
        {
            var isEnrolled = await IsUserEnrolledAsync(userId, courseId);

            var videos = await _context.CourseVideos
                .Where(v => v.CourseId == courseId && v.IsActive)
                .OrderBy(v => v.OrderIndex)
                .ToListAsync();

            //// اگر کاربر ثبت‌نام نکرده، فقط ویدیوهای رایگان نمایش داده شود
            //if (!isEnrolled)
            //{
            //    videos = videos.Where(v => v.IsFree).ToList();
            //}

            return videos;
        }

        public async Task<List<CoursePdf>> GetCoursePdfsAsync(int courseId, int userId)
        {
            var isEnrolled = await IsUserEnrolledAsync(userId, courseId);

            var pdfs = await _context.CoursePdfs
                .Where(p => p.CourseId == courseId && p.IsActive)
                .OrderByDescending(p => p.CreateDate)
                .ToListAsync();

            // اگر کاربر ثبت‌نام نکرده، فقط PDFهای رایگان نمایش داده شود
            if (!isEnrolled)
            {
                pdfs = pdfs.Where(p => p.Price == 0).ToList();
            }

            return pdfs;
        }

        #region Video Progress Tracking

        /// <summary>
        /// 💾 ذخیره پیشرفت تماشای ویدیو
        /// </summary>
        public async Task SaveVideoProgressAsync(int userId, int videoId, double position, double duration, bool completed)
        {
            var progress = await _context.VideoProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.VideoId == videoId);

            if (progress == null)
            {
                progress = new VideoProgress
                {
                    UserId = userId,
                    VideoId = videoId,
                    LastPosition = position,
                    Duration = duration,
                    Completed = completed,
                    WatchedPercent = duration > 0 ? (position / duration) * 100 : 0,
                    LastWatched = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.VideoProgresses.Add(progress);
            }
            else
            {
                progress.LastPosition = position;
                progress.Duration = duration;
                progress.WatchedPercent = duration > 0 ? (position / duration) * 100 : 0;
                progress.LastWatched = DateTime.UtcNow;

                // فقط اگر واقعاً کامل شده باشد (95%+)
                if (completed || progress.WatchedPercent >= 95)
                {
                    progress.Completed = true;
                    progress.CompletedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // به‌روزرسانی درصد پیشرفت کل دوره
            await UpdateCourseProgressAsync(userId, videoId);
        }

        /// <summary>
        /// 📖 دریافت پیشرفت تماشای ویدیو
        /// </summary>
        public async Task<VideoProgressDto?> GetVideoProgressAsync(int userId, int videoId)
        {
            var progress = await _context.VideoProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.VideoId == videoId);

            if (progress == null)
                return null;

            return new VideoProgressDto
            {
                UserId = progress.UserId,
                VideoId = progress.VideoId,
                LastPosition = progress.LastPosition,
                Duration = progress.Duration,
                Completed = progress.Completed,
                WatchedPercent = progress.WatchedPercent,
                LastWatched = progress.LastWatched
            };
        }

        /// <summary>
        /// 📊 به‌روزرسانی درصد پیشرفت کل دوره
        /// </summary>
        private async Task UpdateCourseProgressAsync(int userId, int videoId)
        {
            var video = await _context.CourseVideos.FindAsync(videoId);
            if (video == null) return;

            var courseId = video.CourseId;

            // تعداد کل ویدیوهای دوره
            var totalVideos = await _context.CourseVideos
                .CountAsync(v => v.CourseId == courseId && v.IsActive);

            if (totalVideos == 0) return;

            // تعداد ویدیوهای تماشا شده (بیش از 80%)
            var watchedVideos = await _context.VideoProgresses
                .CountAsync(p => p.UserId == userId
                    && p.Video.CourseId == courseId
                    && p.WatchedPercent >= 80);

            var progressPercent = (int)((double)watchedVideos / totalVideos * 100);

            // به‌روزرسانی enrollment
            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);

            if (enrollment != null)
            {
                enrollment.ProgressPercentage = progressPercent;
                enrollment.IsCompleted = progressPercent >= 100;

                if (enrollment.IsCompleted && enrollment.CompletionDate == null)
                {
                    enrollment.CompletionDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }
        }



        #endregion
    }
}
