using Application.Interfaces;
using Application.Interfaces;
using Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class SuspiciousActivityService : ISuspiciousActivityService
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<SuspiciousActivityService> _logger;

        public SuspiciousActivityService(
            IApplicationDbContext context,
            ILogger<SuspiciousActivityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> LogSuspiciousActivityAsync(
            int userId,
            string activityType,
            int suspicionScore,
            string reason,
            string? ipAddress = null,
            string? userAgent = null,
            int? courseId = null,
            int? videoId = null,
            string? videoFileName = null,
            string? additionalData = null)
        {
            try
            {
                var activity = new SuspiciousActivity
                {
                    UserId = userId,
                    ActivityType = activityType,
                    SuspicionScore = suspicionScore,
                    Reason = reason,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CourseId = courseId,
                    VideoId = videoId,
                    VideoFileName = videoFileName,
                    AdditionalData = additionalData,
                    DetectedAt = DateTime.UtcNow,
                    ResultedInBlock = false,
                    ReviewedByAdmin = false
                };

                await _context.SuspiciousActivities.AddAsync(activity);
                await _context.SaveChangesAsync();

                _logger.LogWarning(
                    "🚨 Suspicious Activity Detected | UserId: {UserId} | Type: {Type} | Score: {Score} | Reason: {Reason}",
                    userId, activityType, suspicionScore, reason);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging suspicious activity for user {UserId}", userId);
                return false;
            }
        }

        public async Task<(bool shouldLock, int count, string reason)> CheckAndLockAccountIfNeededAsync(
            int userId,
            int threshold = 5)
        {
            try
            {
                // بررسی آیا اکانت قبلاً قفل است
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return (false, 0, "User not found");
                }

                if (user.IsLockedForSuspiciousActivity)
                {
                    return (false, 0, "Account already locked");
                }

                // شمارش رفتارهای مشکوک در 30 روز اخیر
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                var suspiciousActivities = await _context.SuspiciousActivities
                    .Where(sa => sa.UserId == userId && sa.DetectedAt >= thirtyDaysAgo)
                    .ToListAsync();

                var count = suspiciousActivities.Count;

                if (count >= threshold)
                {
                    // قفل کردن اکانت
                    user.IsLockedForSuspiciousActivity = true;
                    user.LockedAt = DateTime.UtcNow;
                    user.SuspiciousActivityCount = count;
                    user.LockReason = $"اکانت به دلیل {count} رفتار مشکوک در 30 روز اخیر به صورت خودکار قفل شد. برای باز کردن با پشتیبانی تماس بگیرید.";

                    // ثبت در تاریخچه
                    var lockHistory = new AccountLockHistory
                    {
                        UserId = userId,
                        Action = "Locked",
                        Reason = $"Automatic lock due to {count} suspicious activities",
                        SuspiciousActivityCount = count,
                        ActionDate = DateTime.UtcNow
                    };

                    await _context.AccountLockHistories.AddAsync(lockHistory);

                    // علامت‌گذاری تمام رفتارهای مشکوک که منجر به قفل شدند
                    foreach (var activity in suspiciousActivities)
                    {
                        activity.ResultedInBlock = true;
                        activity.BlockReason = $"Account locked after {count} suspicious activities";
                    }

                    await _context.SaveChangesAsync();

                    _logger.LogCritical(
                        "🔒 ACCOUNT LOCKED | UserId: {UserId} | Suspicious Activities: {Count} | Reason: {Reason}",
                        userId, count, user.LockReason);

                    return (true, count, user.LockReason);
                }

                return (false, count, $"Count {count} is below threshold {threshold}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and locking account for user {UserId}", userId);
                return (false, 0, $"Error: {ex.Message}");
            }
        }

        public async Task<int> GetSuspiciousActivityCountAsync(int userId, int daysBack = 30)
        {
            var startDate = DateTime.UtcNow.AddDays(-daysBack);
            return await _context.SuspiciousActivities
                .Where(sa => sa.UserId == userId && sa.DetectedAt >= startDate)
                .CountAsync();
        }

        public async Task<bool> UnlockAccountAsync(int userId, int adminId, string adminNotes)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot unlock account: User {UserId} not found", userId);
                    return false;
                }

                if (!user.IsLockedForSuspiciousActivity)
                {
                    _logger.LogWarning("Cannot unlock account: User {UserId} is not locked", userId);
                    return false;
                }

                // باز کردن اکانت
                user.IsLockedForSuspiciousActivity = false;
                user.LockReason = null;

                // ثبت در تاریخچه
                var unlockHistory = new AccountLockHistory
                {
                    UserId = userId,
                    Action = "Unlocked",
                    Reason = "Unlocked by administrator",
                    SuspiciousActivityCount = user.SuspiciousActivityCount,
                    ActionDate = DateTime.UtcNow,
                    PerformedByAdminId = adminId,
                    AdminNotes = adminNotes
                };

                await _context.AccountLockHistories.AddAsync(unlockHistory);

                // ریست تعداد رفتارهای مشکوک
                user.SuspiciousActivityCount = 0;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "🔓 Account Unlocked | UserId: {UserId} | By Admin: {AdminId} | Notes: {Notes}",
                    userId, adminId, adminNotes);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking account for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<Domain.Entities.User.User>> GetLockedUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsLockedForSuspiciousActivity)
                .OrderByDescending(u => u.LockedAt)
                .ToListAsync();
        }

        public async Task<List<SuspiciousActivity>> GetUserSuspiciousActivitiesAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 20)
        {
            return await _context.SuspiciousActivities
                .Where(sa => sa.UserId == userId)
                .OrderByDescending(sa => sa.DetectedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<AccountLockHistory>> GetAccountLockHistoryAsync(int userId)
        {
            return await _context.AccountLockHistories
                .Where(alh => alh.UserId == userId)
                .OrderByDescending(alh => alh.ActionDate)
                .ToListAsync();
        }

        public async Task<bool> IsAccountLockedAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user?.IsLockedForSuspiciousActivity ?? false;
        }

        public async Task<bool> ReviewActivityAsync(int activityId, int adminId, string adminNotes)
        {
            try
            {
                var activity = await _context.SuspiciousActivities.FindAsync(activityId);
                if (activity == null)
                {
                    return false;
                }

                activity.ReviewedByAdmin = true;
                activity.ReviewedByAdminId = adminId;
                activity.ReviewedAt = DateTime.UtcNow;
                activity.AdminNotes = adminNotes;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Activity {ActivityId} reviewed by admin {AdminId}",
                    activityId, adminId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reviewing activity {ActivityId}", activityId);
                return false;
            }
        }

        public async Task<int> AutoUnlockExpiredAccountsAsync(int hoursLocked = 24)
        {
            try
            {
                var unlockThreshold = DateTime.UtcNow.AddHours(-hoursLocked);

                // پیدا کردن اکانت‌های قفل شده که بیش از 24 ساعت گذشته
                var expiredLocks = await _context.Users
                    .Where(u => u.IsLockedForSuspiciousActivity &&
                               u.LockedAt.HasValue &&
                               u.LockedAt.Value <= unlockThreshold)
                    .ToListAsync();

                if (!expiredLocks.Any())
                {
                    return 0;
                }

                int unlockedCount = 0;

                foreach (var user in expiredLocks)
                {
                    // باز کردن اکانت
                    user.IsLockedForSuspiciousActivity = false;
                    user.LockReason = null;

                    // ثبت در تاریخچه
                    var unlockHistory = new AccountLockHistory
                    {
                        UserId = user.UserId,
                        Action = "AutoUnlocked",
                        Reason = $"Automatic unlock after {hoursLocked} hours",
                        SuspiciousActivityCount = user.SuspiciousActivityCount,
                        ActionDate = DateTime.UtcNow,
                        AdminNotes = "System automatically unlocked the account after the lock period expired"
                    };

                    await _context.AccountLockHistories.AddAsync(unlockHistory);

                    // ریست تعداد رفتارهای مشکوک
                    user.SuspiciousActivityCount = 0;

                    unlockedCount++;

                    _logger.LogInformation(
                        "🔓 Auto-Unlocked | UserId: {UserId} | Locked for: {Hours} hours",
                        user.UserId, (DateTime.UtcNow - user.LockedAt.Value).TotalHours);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "✅ Auto-Unlock completed | Total unlocked: {Count}",
                    unlockedCount);

                return unlockedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-unlock process");
                return 0;
            }
        }

        public async Task<List<SuspiciousActivity>> GetAllSystemSuspiciousActivitiesAsync(int pageNumber = 1, int pageSize = 50)
        {
            return await _context.SuspiciousActivities
                // .Include(sa => sa.User) // در صورت وجود Navigation Property برای نمایش نام کاربر فعال کنید
                .OrderByDescending(sa => sa.DetectedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }
}
