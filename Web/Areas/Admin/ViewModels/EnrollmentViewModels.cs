using System.ComponentModel.DataAnnotations;

namespace Web.Areas.Admin.ViewModels
{
    // ── Request DTOs (JSON body) ─────────────────────────────────────────────

    public class AddEnrollmentRequest
    {
        [Required]
        public int UserId { get; set; }
        [Required]
        public int CourseId { get; set; }
        public bool IsPaid { get; set; }
    }

    public class RemoveEnrollmentRequest
    {
        [Required]
        public int EnrollmentId { get; set; }
    }

    public class BulkRevokeRequest
    {
        [Required]
        public int CourseId { get; set; }
    }

    // ── View Models ──────────────────────────────────────────────────────────

    public class EnrollmentIndexViewModel
    {
        public List<EnrollmentRowViewModel> Enrollments { get; set; } = new();
        public EnrollmentFilterViewModel Filter { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        public List<CourseSelectItem> Courses { get; set; } = new();
    }

    public class EnrollmentRowViewModel
    {
        public int EnrollmentId { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? UserEmail { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public bool CourseIsActive { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public int ProgressPercentage { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletionDate { get; set; }
        public bool IsPaid { get; set; }
        public decimal? PaymentAmount { get; set; }
    }

    public class EnrollmentFilterViewModel
    {
        public string? SearchQuery { get; set; }
        public int? CourseId { get; set; }
        public bool? IsCompleted { get; set; }
        public bool? IsPaid { get; set; }
        public string SortBy { get; set; } = "date_desc";
    }

    public class CourseSelectItem
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int EnrollmentCount { get; set; }
    }

    // ── Bulk Transfer ────────────────────────────────────────────────────────

    public class BulkTransferViewModel
    {
        [Required(ErrorMessage = "Source course is required")]
        [Display(Name = "From Course")]
        public int? FromCourseId { get; set; }

        [Required(ErrorMessage = "Destination course is required")]
        [Display(Name = "To Course")]
        public int? ToCourseId { get; set; }

        [Display(Name = "Override existing enrollments")]
        public bool OverrideExisting { get; set; } = false;

        [Display(Name = "Copy payment info")]
        public bool CopyPaymentInfo { get; set; } = true;

        [Display(Name = "Remove from source course after transfer")]
        public bool RemoveFromSource { get; set; } = false;

        public List<CourseSelectItem> AvailableCourses { get; set; } = new();

        // Result (set after POST)
        public BulkTransferResult? Result { get; set; }
    }

    public class BulkTransferResult
    {
        public int Transferred { get; set; }
        public int Skipped { get; set; }
        public int Removed { get; set; }
        public List<string> Errors { get; set; } = new();
        public bool IsSuccess => !Errors.Any();
    }
}
