using System.ComponentModel.DataAnnotations;

namespace Domain.Entities.Course
{
    public class CourseEnrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        public int CourseId { get; set; }

        public int UserId { get; set; }

        public DateTime EnrollmentDate { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime? CompletionDate { get; set; }

        public int ProgressPercentage { get; set; }

        public bool IsPaid { get; set; }

        public DateTime? PaymentDate { get; set; }

        public decimal? PaymentAmount { get; set; }

        #region Relations

        public virtual Course Course { get; set; } = null!;
        public virtual User.User User { get; set; } = null!;

        #endregion
    }
}
