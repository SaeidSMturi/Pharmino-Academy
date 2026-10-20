using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundServices
{
    public class AutoDeleteLogsBackgroundService : BackgroundService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AutoDeleteLogsBackgroundService> _logger;
        private readonly string _logsRootPath;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // بررسی هر ۲۴ ساعت یکبار

        public AutoDeleteLogsBackgroundService(IWebHostEnvironment env, ILogger<AutoDeleteLogsBackgroundService> logger)
        {
            _env = env;
            _logger = logger;
            _logsRootPath = Path.Combine(_env.ContentRootPath, "Logs");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔄 سرویس پاکسازی خودکار لاگ‌های قدیمی فعال شد.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DeleteOldLogFiles();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطایی در حین اجرای حذف خودکار فایل‌های لاگ رخ داد.");
                }

                // انتظار تا دور بعدی بررسی (۲۴ ساعت بعد)
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private void DeleteOldLogFiles()
        {
            if (!Directory.Exists(_logsRootPath)) return;

            var logFiles = Directory.GetFiles(_logsRootPath, "*.txt", SearchOption.AllDirectories);
            var cutoffDate = DateTime.Now.AddDays(-30);

            int deletedCount = 0;

            foreach (var filePath in logFiles)
            {
                var fileInfo = new FileInfo(filePath);

                // بررسی تاریخ آخرین ویرایش فایل یا تاریخ ساخت آن
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    fileInfo.Delete();
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation($"🧹 تعداد {deletedCount} فایل لاگ قدیمی‌تر از ۳۰ روز به صورت خودکار حذف شدند.");
            }
        }
    }
}
