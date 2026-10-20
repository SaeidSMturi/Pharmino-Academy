using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Security
{
    /// <summary>
    /// تاریخچه قفل شدن و باز شدن اکانت‌ها
    /// </summary>
    public class AccountLockHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty; // "Locked", "Unlocked"

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public int SuspiciousActivityCount { get; set; } // تعداد رفتارهای مشکوک موقع قفل شدن

        public DateTime ActionDate { get; set; } = DateTime.Now;

        public int? PerformedByAdminId { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(1000)]
        public string? AdminNotes { get; set; }

        #region Relations

        [ForeignKey("UserId")]
        public virtual User.User User { get; set; } = null!;

        [ForeignKey("PerformedByAdminId")]
        public virtual User.User? PerformedByAdmin { get; set; }

        #endregion
    }
}
