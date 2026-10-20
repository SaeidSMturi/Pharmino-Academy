namespace Domain.Configuration
{
    /// <summary>
    /// تنظیمات مدیریت نشست همزمان و جلوگیری از اشتراک‌گذاری اکانت (مبتنی بر SQL Server و EF Core)
    /// Configuration for the Concurrent Session Management and Anti-Account-Sharing system. Binds to the
    /// "SessionSecurity" section in appsettings.json so the behavior can be tuned dynamically per environment
    /// without any code changes. All session state is persisted in SQL Server via Entity Framework Core —
    /// there is no external caching/infrastructure dependency.
    /// </summary>
    public class SessionSecurityOptions
    {
        public const string SectionName = "SessionSecurity";

        /// <summary>
        /// حداکثر تعداد ورود همزمان مجاز برای هر کاربر؛ فراتر از این مقدار قدیمی‌ترین نشست باطل می‌شود
        /// Maximum number of concurrent active sessions allowed per user. When exceeded, the least-recently-
        /// active session(s) are automatically evicted to make room for the new login (anti account-sharing).
        /// </summary>
        public int MaxConcurrentLogins { get; set; } = 2;

        /// <summary>
        /// مدت اعتبار (به ساعت) هر نشست از آخرین فعالیت؛ به صورت Sliding در هر درخواست معتبر تمدید می‌شود
        /// Sliding session lifetime, in hours, measured from the last validated activity. Refreshed on every
        /// successfully validated request so active users are not logged out mid-session.
        /// </summary>
        public int SessionExpirationHours { get; set; } = 12;

        /// <summary>
        /// آستانه سرعت (کیلومتر بر ساعت) برای تشخیص «سفر غیرممکن»؛ عبور از این مقدار به عنوان رفتار مشکوک ثبت می‌شود
        /// Speed threshold, in km/h, above which travel between two consecutive login locations is considered
        /// physically implausible and flagged as suspicious activity (default 800 km/h ~ commercial flight speed).
        /// </summary>
        public double ImpossibleTravelSpeedThresholdKmH { get; set; } = 800;

        /// <summary>
        /// حداقل فاصله (کیلومتر) بین دو IP برای در نظر گرفتن به عنوان کاندید سفر غیرممکن (کاهش نویز شهری/ISP)
        /// Minimum distance, in kilometers, between two consecutive login locations before the impossible-travel
        /// heuristic is even considered — filters out noise from ISP-level IP rotation within the same metro area.
        /// </summary>
        public double MinimumDistanceKmForEvaluation { get; set; } = 50;

        /// <summary>
        /// مدت نگهداری آخرین موقعیت ورود کاربر در SQL Server (روز) جهت مقایسه در ورودهای بعدی
        /// Retention period, in days, for the user's last known login location/IP snapshot (stored in the
        /// UserLastKnownLocations table) used to evaluate future Impossible-Travel checks.
        /// </summary>
        public int LastKnownLocationRetentionDays { get; set; } = 30;

        /// <summary>
        /// فعال‌سازی قفل سخت‌گیرانه IP/Fingerprint؛ در صورت true هرگونه تغییر بلافاصله نشست را باطل می‌کند
        /// When true, any mid-session change in client IP address or device fingerprint immediately invalidates
        /// the session on the very next request (strict binding). Can be relaxed operationally if false-positive
        /// logouts from mobile network switching become disruptive.
        /// </summary>
        public bool EnableStrictIpLocking { get; set; } = true;

        /// <summary>
        /// فاصله زمانی (به دقیقه) بین اجرای دوره‌ای پاکسازی نشست‌های منقضی‌شده و اسنپ‌شات‌های قدیمی موقعیت مکانی
        /// Interval, in minutes, between runs of <see cref="Web.Services.SessionCleanupBackgroundService"/>,
        /// which purges expired UserActiveSessions rows and stale UserLastKnownLocations snapshots from SQL
        /// Server (replacing the automatic per-key TTL expiry Redis previously provided for free).
        /// </summary>
        public int CleanupIntervalMinutes { get; set; } = 30;
    }
}
