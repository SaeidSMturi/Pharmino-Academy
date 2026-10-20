using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities.Security
{
    /// <summary>
    /// آخرین موقعیت جغرافیایی شناخته‌شده ورود هر کاربر - ذخیره‌شده در SQL Server به جای Redis
    /// SQL Server-backed snapshot of a user's most recent login location (one row per user), replacing the
    /// Redis-backed "last known location" key previously used by
    /// <see cref="Web.Services.IImpossibleTravelDetectionService"/> to detect physically-implausible travel
    /// between two consecutive logins from different IP addresses.
    /// </summary>
    public class UserLastKnownLocation
    {
        [Key]
        public int Id { get; set; }

        /// <summary>شناسه کاربر (یکتا - هر کاربر فقط یک رکورد آخرین موقعیت دارد) / Owning user's identifier (unique — one row per user).</summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>آدرس IP در آخرین ورود ثبت‌شده / Client IP address captured at the last recorded login.</summary>
        [Required]
        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>عرض جغرافیایی محل ورود / Latitude of the resolved login location.</summary>
        public double Latitude { get; set; }

        /// <summary>طول جغرافیایی محل ورود / Longitude of the resolved login location.</summary>
        public double Longitude { get; set; }

        /// <summary>زمان ثبت این موقعیت (UTC) / Timestamp (UTC) this location snapshot was recorded.</summary>
        public DateTime TimestampUtc { get; set; }

        #region Relations

        [ForeignKey(nameof(UserId))]
        public virtual User.User? User { get; set; }

        #endregion
    }
}
