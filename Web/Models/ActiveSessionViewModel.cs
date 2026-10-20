namespace Web.Models
{
    /// <summary>
    /// مدل نمایشی یک نشست فعال برای داشبورد «نشست‌های فعال کاربر» - شامل اطلاعات انسان‌خوان دستگاه
    /// Display model for a single row on the "Active Sessions" dashboard (Feature 4). Wraps a raw
    /// <see cref="Application.DTOs.User.SessionRecord"/> with human-readable OS/Browser names (parsed via
    /// <see cref="Application.Interfaces.IUserAgentParserService"/>) and a flag indicating the session belonging to
    /// the currently-authenticated request, so the view can visually mark "This device".
    /// </summary>
    public class ActiveSessionViewModel
    {
        public string SessionId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string Browser { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>آیا این نشست همان نشستی است که درخواست جاری با آن احراز هویت شده / Whether this row represents the caller's own current session.</summary>
        public bool IsCurrentSession { get; set; }
    }
}
