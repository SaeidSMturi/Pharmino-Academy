using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Course;

public class CoursePdf
{
    [Key]
    public int PdfId { get; set; }

    [Required]
    public int CourseId { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string OriginalFileName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? LastUpdate { get; set; }

    #region Relations

    public virtual Course Course { get; set; } = null!;

    #endregion
}
