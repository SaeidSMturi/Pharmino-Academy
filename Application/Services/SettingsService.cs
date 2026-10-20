using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "settings_all";
        private const int CacheDurationMinutes = 5; // کاهش به 5 دقیقه برای بروزرسانی سریع‌تر

        public SettingsService(IApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<List<Setting>> GetAllSettingsAsync()
        {
            if (!_cache.TryGetValue(CacheKey, out List<Setting>? settings))
            {
                settings = await _context.Settings
                    .AsNoTracking()
                    .OrderBy(s => s.Category)
                    .ThenBy(s => s.DisplayOrder)
                    .ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheDurationMinutes),

                    // این خط مشکل را حل می‌کند.
                    // چون واحد سایز را در Program.cs بایت در نظر گرفتیم، اینجا هم یک عدد تخمینی به بایت می‌دهیم.
                    Size = 2048 // تخمین 2 کیلوبایت برای لیست تنظیمات
                };

                _cache.Set(CacheKey, settings, cacheOptions);
            }

            return settings ?? new List<Setting>();
        }

        public async Task<Setting?> GetSettingByIdAsync(int id)
        {
            var settings = await GetAllSettingsAsync();
            return settings.FirstOrDefault(s => s.Id == id);
        }

        public async Task<Setting?> GetSettingByKeyAsync(string key)
        {
            var settings = await GetAllSettingsAsync();
            return settings.FirstOrDefault(s => s.Title == key);
        }

        public async Task<string?> GetStringSettingAsync(string key, string? defaultValue = null)
        {
            var setting = await GetSettingByKeyAsync(key);
            return setting?.DataString ?? defaultValue;
        }

        public async Task<bool?> GetBoolSettingAsync(string key, bool? defaultValue = null)
        {
            var setting = await GetSettingByKeyAsync(key);
            return setting?.DataBool ?? defaultValue;
        }

        public async Task<int?> GetIntSettingAsync(string key, int? defaultValue = null)
        {
            var setting = await GetSettingByKeyAsync(key);
            return setting?.DataInt ?? defaultValue;
        }

        public async Task<decimal?> GetDecimalSettingAsync(string key, decimal? defaultValue = null)
        {
            var setting = await GetSettingByKeyAsync(key);
            return setting?.DataDecimal ?? defaultValue;
        }

        public async Task UpdateSettingAsync(int id, Setting setting)
        {
            var existingSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Id == id);
            if (existingSetting != null)
            {
                existingSetting.DataString = setting.DataString;
                existingSetting.DataBool = setting.DataBool;
                existingSetting.DataInt = setting.DataInt;
                existingSetting.DataDecimal = setting.DataDecimal;
                existingSetting.DataJson = setting.DataJson;
                existingSetting.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await InvalidateCacheAsync();
            }
        }

        public async Task InvalidateCacheAsync()
        {
            _cache.Remove(CacheKey);
            await Task.CompletedTask;
        }

        public async Task<Dictionary<string, object>> GetSettingsByGroupAsync(string group)
        {
            var settings = await GetAllSettingsAsync();
            return settings
                .Where(s => s.Category == group)
                .OrderBy(s => s.DisplayOrder)
                .ToDictionary(s => s.Title, s => (object)(s.DataString ?? s.DataBool ?? s.DataInt ?? s.DataDecimal ?? (object)""));
        }
    }
}
