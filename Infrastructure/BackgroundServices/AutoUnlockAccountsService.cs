using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundServices
{
    /// <summary>
    /// سرویس پس‌زمینه برای باز کردن خودکار اکانت‌های قفل شده بعد از 24 ساعت
    /// </summary>
    public class AutoUnlockAccountsService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoUnlockAccountsService> _logger;
        private readonly TimeSpan _checkInterval;
        private readonly int _unlockAfterHours;

        public AutoUnlockAccountsService(
            IServiceProvider serviceProvider,
            ILogger<AutoUnlockAccountsService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            // خواندن تنظیمات از appsettings.json
            _checkInterval = TimeSpan.FromMinutes(
                configuration.GetValue<int>("AccountLockSettings:AutoUnlockCheckIntervalMinutes", 60));

            _unlockAfterHours = configuration.GetValue<int>(
                "AccountLockSettings:AutoUnlockAfterHours", 24);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "🔄 AutoUnlockAccountsService started | Check interval: {Interval} minutes | Unlock after: {Hours} hours",
                _checkInterval.TotalMinutes, _unlockAfterHours);

            // تاخیر اولیه برای اطمینان از آماده بودن application
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndUnlockExpiredAccountsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in AutoUnlockAccountsService");
                }

                // صبر تا چک بعدی
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("AutoUnlockAccountsService stopped");
        }

        private async Task CheckAndUnlockExpiredAccountsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var suspiciousActivityService = scope.ServiceProvider
                    .GetRequiredService<ISuspiciousActivityService>();

                var unlockedCount = await suspiciousActivityService
                    .AutoUnlockExpiredAccountsAsync(_unlockAfterHours);

                if (unlockedCount > 0)
                {
                    _logger.LogInformation(
                        "✅ Auto-unlocked {Count} account(s)",
                        unlockedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and unlocking expired accounts");
            }
        }
    }
}
