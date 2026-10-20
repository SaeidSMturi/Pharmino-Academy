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

        public int? DurationHours { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreateDate { get; set; }

        public DateTime? LastUpdate { get; set; }

        #region Relations

        public virtual ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
        public virtual ICollection<CourseVideo> Videos { get; set; } = new List<CourseVideo>();
        public virtual ICollection<CoursePdf> Pdfs { get; set; } = new List<CoursePdf>();

        #endregion
    }
}
