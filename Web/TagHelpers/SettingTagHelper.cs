using Infrastructure.Services;
using Application.Interfaces;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;

namespace Web.TagHelpers
{
    // ۱. تعریف یک Enum برای تشخیص نوع داده
    public enum SettingDataType
    {
        String,
        Int,
        Bool,
        Decimal
    }

    [HtmlTargetElement("setting")]
    public class SmartSettingTagHelper : TagHelper
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<SmartSettingTagHelper> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public SmartSettingTagHelper(
            ISettingsService settingsService,
            ILogger<SmartSettingTagHelper> logger,
            IConfiguration configuration,
            IMemoryCache cache)
        {
            _settingsService = settingsService;
            _logger = logger;
            _configuration = configuration;
            _cache = cache;
        }

        [HtmlAttributeName("key")]
        public string Key { get; set; } = string.Empty;

        // ۲. نوع داده به صورت پیش‌فرض رشته در نظر گرفته می‌شود
        [HtmlAttributeName("type")]
        public SettingDataType DataType { get; set; } = SettingDataType.String;

        // ۳. مقدار پیش‌فرض به شکل استرینگ دریافت می‌شود
        [HtmlAttributeName("default")]
        public string? Default { get; set; }

        [HtmlAttributeName("format")]
        public string? Format { get; set; }

        [HtmlAttributeName("as")]
        public string? As { get; set; }

        [HtmlAttributeName("fallback-to-config")]
        public bool FallbackToConfig { get; set; } = false;

        [HtmlAttributeName("cache-minutes")]
        public int CacheMinutes { get; set; } = 60;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = string.IsNullOrWhiteSpace(As) ? null : As;

            if (string.IsNullOrWhiteSpace(Key))
            {
                _logger.LogWarning("Setting TagHelper called without a valid 'key'.");
                output.Content.SetContent(Default ?? string.Empty);
                return;
            }

            try
            {
                string cacheKey = $"Setting_{DataType}_{Key}";

                var value = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheMinutes);

                    // فراخوانی متد هوشمند برای خواندن دیتابیس
                    var dbValue = await FetchValueByTypeAsync();

                    // تصمیم‌گیری برای مقدار نهایی
                    string? finalValue = dbValue;
                    if (dbValue == null && FallbackToConfig)
                    {
                        finalValue = _configuration.GetValue<string>(Key);
                    }

                    // 👈 بخش اضافه شده: تعیین سایز برای رفع خطای InvalidOperationException
                    // طول رشته ضرب‌در ۲ (چون هر کاراکتر ۲ بایت است). حداقل سایز ۱ بایت لحاظ می‌شود.
                    entry.Size = string.IsNullOrEmpty(finalValue) ? 1 : finalValue.Length * 2;

                    return finalValue;
                });

                FormatAndSetOutput(output, value ?? Default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve setting for key: {Key}", Key);
                output.Content.SetContent(FallbackToConfig ? (_configuration.GetValue<string>(Key) ?? Default ?? "") : (Default ?? ""));
            }
        }

        /// <summary>
        /// متد هوشمند برای تبدیل مقادیر و فراخوانی سرویس مناسب
        /// </summary>
        private async Task<string?> FetchValueByTypeAsync()
        {
            switch (DataType)
            {
                case SettingDataType.Int:
                    int? defaultInt = int.TryParse(Default, out int i) ? i : null;
                    var intVal = await _settingsService.GetIntSettingAsync(Key, defaultInt);
                    return intVal?.ToString();

                case SettingDataType.Bool:
                    bool? defaultBool = bool.TryParse(Default, out bool b) ? b : null;
                    var boolVal = await _settingsService.GetBoolSettingAsync(Key, defaultBool);
                    return boolVal?.ToString().ToLowerInvariant();

                case SettingDataType.Decimal:
                    decimal? defaultDec = decimal.TryParse(Default, out decimal d) ? d : null;
                    var decVal = await _settingsService.GetDecimalSettingAsync(Key, defaultDec);
                    return decVal?.ToString();

                case SettingDataType.String:
                default:
                    return await _settingsService.GetStringSettingAsync(Key, Default);
            }
        }

        /// <summary>
        /// مدیریت فرمت‌بندی خروجی
        /// </summary>
        private void FormatAndSetOutput(TagHelperOutput output, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                output.Content.SetContent(string.Empty);
                return;
            }

            if (!string.IsNullOrWhiteSpace(Format))
            {
                if (DataType == SettingDataType.Int && int.TryParse(value, out int intVal))
                    output.Content.SetContent(intVal.ToString(Format));
                else if (DataType == SettingDataType.Decimal && decimal.TryParse(value, out decimal decVal))
                    output.Content.SetContent(decVal.ToString(Format));
                else
                    output.Content.SetContent(value);
            }
            else
            {
                output.Content.SetContent(value);
            }
        }
    }
}