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
        public bool IsBundle { get; set; }
        public decimal? DiscountPrice { get; set; }
        public bool IsDiscountActive { get; set; }
        public bool HasActiveDiscount => IsDiscountActive && DiscountPrice.HasValue && DiscountPrice.Value < Price;
        public decimal EffectivePrice => HasActiveDiscount ? DiscountPrice.Value : Price;
        public int DiscountPercent => Price > 0 && HasActiveDiscount ? (int)Math.Round((1 - (DiscountPrice.Value / Price)) * 100) : 0;
        public int SubCourseCount { get; set; }
    }
}
