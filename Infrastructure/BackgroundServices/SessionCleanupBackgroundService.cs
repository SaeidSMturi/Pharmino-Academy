using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundServices
{
    /// <summary>
    /// سرویس پس‌زمینه پاکسازی خودکار نشست‌های منقضی‌شده و اسنپ‌شات‌های قدیمی موقعیت مکانی از SQL Server
    /// </summary>
    public class SessionCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SessionCleanupBackgroundService> _logger;
        private readonly TimeSpan _checkInterval;
        private readonly int _lastKnownLocationRetentionDays;

        public SessionCleanupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<SessionCleanupBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _checkInterval = TimeSpan.FromMinutes(
                configuration.GetValue("SessionSecurity:CleanupIntervalMinutes", 30));

            _lastKnownLocationRetentionDays = configuration.GetValue(
                "SessionSecurity:LastKnownLocationRetentionDays", 30);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "🔄 SessionCleanupBackgroundService started | Check interval: {Interval} minutes.",
                _checkInterval.TotalMinutes);

            // تاخیر اولیه برای اطمینان از آماده بودن application
            // Initial delay to make sure the application has finished starting up.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredRowsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in SessionCleanupBackgroundService");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("SessionCleanupBackgroundService stopped");
        }

        /// <summary>
        /// XML Summary: Both deletes are issued as single bulk <c>ExecuteDeleteAsync</c> statements.
        /// </summary>
        private async Task CleanupExpiredRowsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<WebContext>();

            var nowUtc = DateTime.UtcNow;

            var expiredSessions = await context.UserActiveSessions
                .Where(s => s.ExpiresAtUtc <= nowUtc)
                .ExecuteDeleteAsync();

            var retentionCutoffUtc = nowUtc.AddDays(-_lastKnownLocationRetentionDays);
            var staleLocations = await context.UserLastKnownLocations
                .Where(l => l.TimestampUtc < retentionCutoffUtc)
                .ExecuteDeleteAsync();

            if (expiredSessions > 0 || staleLocations > 0)
            {
                _logger.LogInformation(
                    "🧹 Session cleanup removed {ExpiredSessions} expired session(s) and {StaleLocations} stale last-known-location row(s).",
                    expiredSessions, staleLocations);
            }
        }
    }
}
