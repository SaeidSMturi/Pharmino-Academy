using Domain.Entities.Course;

namespace Application.Interfaces
{
    public interface ICourseService
    {
        Task<List<Course>> GetActiveCoursesAsync();
        Task<List<Course>> GetUserEnrolledCoursesAsync(int userId);
        Task<List<CourseEnrollment>> GetUserEnrollmentsAsync(int userId);
        Task<Course?> GetCourseByIdAsync(int courseId);
        Task<Course?> GetCourseWithDetailsAsync(int courseId);
        Task<Dictionary<int, Domain.Entities.Course.VideoProgress>> GetVideoProgressDictAsync(int userId, int courseId);
        Task<bool> IsUserEnrolledAsync(int userId, int courseId);
        Task<CourseEnrollment?> GetUserEnrollmentAsync(int userId, int courseId);
        Task<bool> EnrollUserInCourseAsync(int userId, int courseId, decimal paymentAmount);
        Task<bool> CanUserAccessVideoAsync(int userId, int videoId);
        Task<bool> CanUserAccessPdfAsync(int userId, int pdfId);
        Task<CourseVideo?> GetVideoByIdAsync(int videoId);
        Task<CoursePdf?> GetPdfByIdAsync(int pdfId);
        Task<List<CourseVideo>> GetCourseVideosAsync(int courseId, int userId);
        Task<List<CoursePdf>> GetCoursePdfsAsync(int courseId, int userId);

        // 🎬 Video Progress Tracking
        Task SaveVideoProgressAsync(int userId, int videoId, double position, double duration, bool completed);
        Task<VideoProgressDto?> GetVideoProgressAsync(int userId, int videoId);
    }

    /// <summary>
    /// مدل پیشرفت تماشای ویدیو
    /// </summary>
    public class VideoProgressDto
    {
        public int UserId { get; set; }
        public int VideoId { get; set; }
        public double LastPosition { get; set; }
        public double Duration { get; set; }
        public bool Completed { get; set; }
        public double WatchedPercent { get; set; }
        public DateTime LastWatched { get; set; }
    }
}
