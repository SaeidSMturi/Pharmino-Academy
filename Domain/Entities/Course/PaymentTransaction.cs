using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Course
{
    public class PaymentTransaction
    {
        [Key]
        public int TransactionId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        [MaxLength(100)]
        public string OrderToken { get; set; } = string.Empty; // The PayPal Order ID / token

        [MaxLength(100)]
        public string? PaymentId { get; set; } // The PayPal Capture/Transaction ID

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(10)]
        public string Currency { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty; // Pending, Completed, Failed, Cancelled

        public DateTime CreateDate { get; set; }

        public DateTime? UpdateDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string Gateway { get; set; } = string.Empty; // PayPal

        [Required]
        [MaxLength(50)]
        public string Mode { get; set; } = string.Empty; // Live, Sandbox, Demo

        #region Relations
        [ForeignKey("UserId")]
        public virtual User.User User { get; set; } = null!;

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; } = null!;
        #endregion
    }
}
