namespace Application.DTOs.User
{
    /// <summary>
    /// حالت (Snapshot) یک نشست فعال کاربر که به عنوان DTO بین لایه سرویس و SQL Server منتقل می‌شود
    /// Plain data-transfer snapshot of a single active user session, returned by
    /// <see cref="Core.Interfaces.ISessionManagerService"/> and backed by the <c>UserActiveSessions</c> table in
    /// SQL Server. This is the authoritative source of truth for concurrent-session enforcement, device
    /// binding, and the "Active Sessions" dashboard.
    /// </summary>
    public class SessionRecord
    {
        /// <summary>
        /// شناسه یکتای نشست - همان مقداری که در Claim "SessionId" کوکی احراز هویت ذخیره می‌شود
        /// Unique session identifier — matches the "SessionId" claim stored in the authentication cookie.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>شناسه کاربر مالک این نشست / Owning user's identifier.</summary>
        public int UserId { get; set; }

        /// <summary>آدرس IP در لحظه ورود / Client IP address captured at login time.</summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>رشته User-Agent خام مرورگر/دستگاه / Raw User-Agent string of the browser/device.</summary>
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// اثر انگشت دستگاه (Hash ترکیب User-Agent و IP) که کوکی به آن Bind می‌شود
        /// Device fingerprint (hash of User-Agent + IP) the authentication cookie is bound to.
        /// </summary>
        public string DeviceFingerprint { get; set; } = string.Empty;

        /// <summary>زمان ایجاد نشست (UTC) / Session creation timestamp (UTC).</summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>زمان آخرین فعالیت معتبر (UTC) - مبنای Sliding Expiration / Last validated activity timestamp (UTC) — basis for sliding expiration.</summary>
        public DateTime LastActivityUtc { get; set; }

        /// <summary>زمان انقضای فعلی نشست (UTC) / Current absolute expiration timestamp (UTC).</summary>
        public DateTime ExpiresAtUtc { get; set; }
    }
}
