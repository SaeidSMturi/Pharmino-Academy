namespace Web.Models
{
    public class HomeIndexViewModel
    {
        public List<HomeCourseDto> LatestCourses { get; set; } = new();
    }

    public class HomeCourseDto
    {
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int DurationHours { get; set; }
        public string? ImageName { get; set; }
        public int VideoCount { get; set; }
    }
}
