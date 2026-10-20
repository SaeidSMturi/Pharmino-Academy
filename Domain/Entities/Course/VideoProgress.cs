using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Course
{
    /// <summary>
    /// 🎬 ذخیره پیشرفت تماشای ویدیو برای هر کاربر
    /// </summary>
    public class VideoProgress
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int VideoId { get; set; }

        /// <summary>
        /// آخرین موقعیت تماشا (ثانیه)
        /// </summary>
        public double LastPosition { get; set; }

        /// <summary>
        /// مدت کل ویدیو (ثانیه)
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// درصد تماشا شده
        /// </summary>
        public double WatchedPercent { get; set; }

        /// <summary>
        /// آیا کامل تماشا شده؟
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// زمان آخرین تماشا
        /// </summary>
        public DateTime LastWatched { get; set; }

        /// <summary>
        /// زمان تکمیل (اگر کامل شده باشد)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// زمان ایجاد رکورد
        /// </summary>
        public DateTime CreatedAt { get; set; }

        // Navigation Properties
        [ForeignKey("UserId")]
        public virtual User.User User { get; set; } = null!;

        [ForeignKey("VideoId")]
        public virtual CourseVideo Video { get; set; } = null!;
    }
}
