using Domain.Entities.Course;

namespace Application.DTOs.User
{
    public class UserDashboardViewModel
    {
        public int TotalCourses { get; set; }
        public int CompletedCourses { get; set; }
        public int ActiveCourses { get; set; }
        public double AverageProgress { get; set; }

        // لیست دوره‌هایی که کاربر اخیراً در آن‌ها ثبت‌نام کرده یا تماشا کرده
        public List<CourseEnrollment> RecentEnrollments { get; set; }
    }
}
