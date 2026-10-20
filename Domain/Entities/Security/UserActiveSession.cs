using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Security
{
    /// <summary>
    /// نشست فعال کاربر ذخیره‌شده در SQL Server - جایگزین کامل ذخیره‌سازی نشست مبتنی بر Redis
    /// SQL Server-backed record of a single active user session. This is the authoritative source of truth
    /// for concurrent-session enforcement (anti account-sharing), IP/device binding, and the "Active Sessions"
    /// dashboard, replacing the previous Redis-backed <c>SessionRecord</c> store. Validated on (almost) every
    /// authenticated request via <see cref="Web.Services.ISessionManagerService"/>, so queries against this
    /// table must stay index-friendly and cheap (see indexes configured in <c>WebContext.OnModelCreating</c>).
    /// </summary>
    public class UserActiveSession
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// شناسه یکتای نشست - همان مقداری که در Claim "SessionId" کوکی احراز هویت ذخیره می‌شود
        /// Unique session identifier — matches the "SessionId" claim stored in the authentication cookie.
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>شناسه کاربر مالک این نشست / Owning user's identifier.</summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>آدرس IP در لحظه ورود / Client IP address captured at login time.</summary>
        [Required]
        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>رشته User-Agent خام مرورگر/دستگاه / Raw User-Agent string of the browser/device.</summary>
        [MaxLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// اثر انگشت دستگاه (Hash ترکیب User-Agent و IP) که کوکی به آن Bind می‌شود
        /// Device fingerprint (hash of User-Agent + IP) the authentication cookie is bound to.
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string DeviceFingerprint { get; set; } = string.Empty;

        /// <summary>زمان ایجاد نشست (UTC) / Session creation timestamp (UTC).</summary>
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>زمان آخرین فعالیت معتبر (UTC) - مبنای Sliding Expiration / Last validated activity timestamp (UTC) — basis for sliding expiration.</summary>
        public DateTime LastActivityUtc { get; set; }

        /// <summary>زمان انقضای فعلی نشست (UTC) / Current absolute expiration timestamp (UTC).</summary>
        public DateTime ExpiresAtUtc { get; set; }

        #region Relations

        [ForeignKey(nameof(UserId))]
        public virtual User.User? User { get; set; }

        #endregion
    }
}
