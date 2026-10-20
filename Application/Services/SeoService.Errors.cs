using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Domain.Entities.Seo;

namespace Application.Services
{
    public partial class SeoService
    {
        public async Task LogErrorAsync(string url, int statusCode, string? referrer, string? userAgent)
        {
            url = NormalizeUrlPath(url);

            // اگر آدرس مربوط به ربات‌ها، مسیرهای سیستمی یا فایل‌های نامرتبط باشد، کلا نادیده گرفته می‌شود
            if (IsExternalOrIgnoredRoute(url))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(referrer)) referrer = null;
            if (string.IsNullOrWhiteSpace(userAgent)) userAgent = null;

            var existing = await _context.SeoErrorLogs
                .FirstOrDefaultAsync(e => e.Url == url && e.StatusCode == statusCode && !e.IsResolved);

            if (existing != null)
            {
                existing.OccurrenceCount++;
                existing.LastOccurred = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(referrer))
                {
                    existing.Referrer = referrer;
                }
                if (!string.IsNullOrWhiteSpace(userAgent))
                {
                    existing.UserAgent = userAgent;
                }
                _context.SeoErrorLogs.Update(existing);
            }
            else
            {
                var log = new SeoErrorLog
                {
                    Url = url,
                    StatusCode = statusCode,
                    Referrer = referrer,
                    UserAgent = userAgent,
                    OccurrenceCount = 1,
                    LastOccurred = DateTime.UtcNow,
                    IsResolved = false
                };
                _context.SeoErrorLogs.Add(log);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<SeoErrorLog>> GetErrorLogsAsync()
        {
            return await _context.SeoErrorLogs
                .OrderByDescending(e => e.LastOccurred)
                .ToListAsync();
        }

        public async Task ResolveErrorLogAsync(int id)
        {
            var log = await _context.SeoErrorLogs.FindAsync(id);
            if (log != null)
            {
                log.IsResolved = true;
                _context.SeoErrorLogs.Update(log);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteErrorLogAsync(int id)
        {
            var log = await _context.SeoErrorLogs.FindAsync(id);
            if (log != null)
            {
                _context.SeoErrorLogs.Remove(log);
                await _context.SaveChangesAsync();
            }
        }

        public async Task PurgeBotErrorsAsync()
        {
            // پاکسازی تمامی خطاهای قبلی که طبق قانون IsExternalOrIgnoredRoute نامعتبر هستند
            var allErrors = await _context.SeoErrorLogs.ToListAsync();
            var botLogs = allErrors.Where(e => IsExternalOrIgnoredRoute(e.Url)).ToList();

            if (botLogs.Any())
            {
                _context.SeoErrorLogs.RemoveRange(botLogs);
                await _context.SaveChangesAsync();
            }
        }

        public async Task PurgeAllResolvedErrorsAsync()
        {
            var resolvedLogs = await _context.SeoErrorLogs
                .Where(e => e.IsResolved)
                .ToListAsync();

            if (resolvedLogs.Any())
            {
                _context.SeoErrorLogs.RemoveRange(resolvedLogs);
                await _context.SaveChangesAsync();
            }
        }
    }
}