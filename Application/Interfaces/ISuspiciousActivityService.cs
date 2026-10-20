using Domain.Entities.Security;

namespace Application.Interfaces
{
    public interface ISuspiciousActivityService
    {
        /// <summary>
        /// ثبت رفتار مشکوک کاربر
        /// </summary>
        Task<bool> LogSuspiciousActivityAsync(
            int userId,
            string activityType,
            int suspicionScore,
            string reason,
            string? ipAddress = null,
            string? userAgent = null,
            int? courseId = null,
            int? videoId = null,
            string? videoFileName = null,
            string? additionalData = null);

        /// <summary>
        /// بررسی و قفل کردن اکانت در صورت تعداد بیش از حد رفتارهای مشکوک
        /// </summary>
        Task<(bool shouldLock, int count, string reason)> CheckAndLockAccountIfNeededAsync(int userId, int threshold = 5);

        /// <summary>
        /// دریافت تعداد رفتارهای مشکوک کاربر در 30 روز اخیر
        /// </summary>
        Task<int> GetSuspiciousActivityCountAsync(int userId, int daysBack = 30);

        /// <summary>
        /// باز کردن اکانت توسط ادمین
        /// </summary>
        Task<bool> UnlockAccountAsync(int userId, int adminId, string adminNotes);

        /// <summary>
        /// دریافت لیست کاربران قفل شده
        /// </summary>
        Task<List<Domain.Entities.User.User>> GetLockedUsersAsync();

        /// <summary>
        /// دریافت تاریخچه رفتارهای مشکوک یک کاربر
        /// </summary>
        Task<List<SuspiciousActivity>> GetUserSuspiciousActivitiesAsync(int userId, int pageNumber = 1, int pageSize = 20);

        /// <summary>
        /// دریافت تاریخچه قفل و باز شدن اکانت
        /// </summary>
        Task<List<AccountLockHistory>> GetAccountLockHistoryAsync(int userId);

        /// <summary>
        /// بررسی اینکه آیا کاربر قفل است
        /// </summary>
        Task<bool> IsAccountLockedAsync(int userId);

        /// <summary>
        /// بررسی ادمین یک رفتار مشکوک
        /// </summary>
        Task<bool> ReviewActivityAsync(int activityId, int adminId, string adminNotes);

        /// <summary>
        /// باز کردن خودکار اکانت‌هایی که بیش از مدت مشخص قفل بوده‌اند
        /// </summary>
        Task<int> AutoUnlockExpiredAccountsAsync(int hoursLocked = 24);

        Task<List<SuspiciousActivity>> GetAllSystemSuspiciousActivitiesAsync(int pageNumber = 1, int pageSize = 50);
    }
}
