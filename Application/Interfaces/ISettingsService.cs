using Domain.Entities;

namespace Application.Interfaces
{
    public interface ISettingsService
    {
        Task<List<Setting>> GetAllSettingsAsync();
        Task<Setting?> GetSettingByIdAsync(int id);
        Task<Setting?> GetSettingByKeyAsync(string key);
        Task<string?> GetStringSettingAsync(string key, string? defaultValue = null);
        Task<bool?> GetBoolSettingAsync(string key, bool? defaultValue = null);
        Task<int?> GetIntSettingAsync(string key, int? defaultValue = null);
        Task<decimal?> GetDecimalSettingAsync(string key, decimal? defaultValue = null);
        Task UpdateSettingAsync(int id, Setting setting);
        Task InvalidateCacheAsync();
        Task<Dictionary<string, object>> GetSettingsByGroupAsync(string group);
    }
}
