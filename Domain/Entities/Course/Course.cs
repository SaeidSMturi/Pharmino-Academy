using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Course
{
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ImageName { get; set; }

        public decimal Price { get; set; }

        public decimal? DiscountPrice { get; set; }

        public bool IsDiscountActive { get; set; } = false;

        public bool IsBundle { get; set; } = false;

        public int? DurationHours { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsListedForSale { get; set; } = true;

        public DateTime CreateDate { get; set; }

        public DateTime? LastUpdate { get; set; }

        #region Computed Helpers

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal EffectivePrice => (IsDiscountActive && DiscountPrice.HasValue && DiscountPrice.Value >= 0 && DiscountPrice.Value < Price)
            ? DiscountPrice.Value
            : Price;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool HasActiveDiscount => IsDiscountActive && DiscountPrice.HasValue && DiscountPrice.Value >= 0 && DiscountPrice.Value < Price;

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int DiscountPercent => HasActiveDiscount && Price > 0
            ? (int)Math.Round((Price - DiscountPrice!.Value) / Price * 100)
            : 0;

        #endregion

        #region Relations

        public virtual ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
        public virtual ICollection<CourseVideo> Videos { get; set; } = new List<CourseVideo>();
        public virtual ICollection<CoursePdf> Pdfs { get; set; } = new List<CoursePdf>();

        // If this course is a Bundle: the sub-courses it contains
        public virtual ICollection<CourseBundleItem> BundleCourses { get; set; } = new List<CourseBundleItem>();

        // If this course is included in other bundles
        public virtual ICollection<CourseBundleItem> IncludedInBundles { get; set; } = new List<CourseBundleItem>();

        #endregion
    }
}
