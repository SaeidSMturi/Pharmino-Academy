using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Course
{
    public class CourseVideo
    {
        [Key]
        public int VideoId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(500)]
        public string VideoFileName { get; set; } = string.Empty;

        public int? DurationSeconds { get; set; }

        public int OrderIndex { get; set; }

        public bool IsFree { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreateDate { get; set; }

        public DateTime? LastUpdate { get; set; }

        #region Relations

        public virtual Course Course { get; set; } = null!;

        #endregion
    }
}
