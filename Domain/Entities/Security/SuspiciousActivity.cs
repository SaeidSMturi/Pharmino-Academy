using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Security
{
    /// <summary>
    /// ثبت رفتارهای مشکوک کاربران در سیستم
    /// </summary>
    public class SuspiciousActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ActivityType { get; set; } = string.Empty; // "VideoDownloadAttempt", "ExcessiveSeek", "SequentialDownload", etc.

        [Required]
        public int SuspicionScore { get; set; } // امتیاز مشکوک بودن (0-100)

        [MaxLength(500)]
        public string? Reason { get; set; } // دلیل مشکوک بودن

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public int? CourseId { get; set; }

        public int? VideoId { get; set; }

        [MaxLength(200)]
        public string? VideoFileName { get; set; }

        public DateTime DetectedAt { get; set; } = DateTime.Now;

        // اطلاعات تکمیلی به صورت JSON
        [MaxLength(2000)]
        public string? AdditionalData { get; set; }

        // آیا این رفتار منجر به محدودیت شد؟
        public bool ResultedInBlock { get; set; }

        [MaxLength(500)]
        public string? BlockReason { get; set; }

        // آیا ادمین این را بررسی کرده؟
        public bool ReviewedByAdmin { get; set; }

        public int? ReviewedByAdminId { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(1000)]
        public string? AdminNotes { get; set; }

        #region Relations

        [ForeignKey("UserId")]
        public virtual User.User User { get; set; } = null!;

        [ForeignKey("ReviewedByAdminId")]
        public virtual User.User? Reviewer { get; set; }

        #endregion
    }
}
