namespace Web.ViewModels.Admin
{
    public class SuspiciousActivityViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string ActivityType { get; set; }
        public int SuspicionScore { get; set; }
        public string Reason { get; set; }
        public string? IpAddress { get; set; }
        public int? VideoId { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool ResultedInBlock { get; set; }
        public bool ReviewedByAdmin { get; set; }
    }

    public class LockedAccountViewModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? LockedAt { get; set; }
        public int SuspiciousActivityCount { get; set; }
        public string? LockReason { get; set; }
    }
}
