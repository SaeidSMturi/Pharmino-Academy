using Microsoft.AspNetCore.Http;

namespace Application.Interfaces
{
    /// <summary>
    /// 🔐 سرویس Browser Fingerprinting فوق پیشرفته
    /// تولید و اعتبارسنجی اثر انگشت مرورگر برای جلوگیری از token theft
    /// </summary>
    public interface IBrowserFingerprintService
    {
        string GenerateServerSideFingerprint(HttpContext context);
        bool ValidateFingerprint(string clientFingerprint, string serverFingerprint, int tolerance = 3);
        string HashFingerprint(string fingerprint);
        FingerprintAnalysis AnalyzeFingerprint(BrowserFingerprint fingerprint);
        bool IsKnownDownloaderFingerprint(BrowserFingerprint fingerprint);
        void RecordFingerprint(int userId, BrowserFingerprint fingerprint);
        bool HasFingerprintAnomaly(int userId, BrowserFingerprint currentFingerprint);
    }

    #region Models

    /// <summary>
    /// مدل اثر انگشت مرورگر
    /// </summary>
    public class BrowserFingerprint
    {
        public string UserAgent { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string[] Languages { get; set; } = Array.Empty<string>();
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int ColorDepth { get; set; }
        public string Timezone { get; set; } = string.Empty;
        public int TimezoneOffset { get; set; }
        public bool CookiesEnabled { get; set; }
        public string CanvasHash { get; set; } = string.Empty;
        public string WebGLVendor { get; set; } = string.Empty;
        public string WebGLRenderer { get; set; } = string.Empty;
        public int HardwareConcurrency { get; set; }
        public double DeviceMemory { get; set; }
        public int PluginCount { get; set; }
        public bool TouchSupport { get; set; }
        public int TouchPoints { get; set; }
        public bool IsMobile { get; set; }
        public string AudioContextHash { get; set; } = string.Empty;
        public string FontsHash { get; set; } = string.Empty;
        public bool DevToolsOpen { get; set; }
        public bool HasAutomationFlags { get; set; }
        public string BatteryStatus { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = string.Empty;
        public bool DoNotTrack { get; set; }
        public string ClientHash { get; set; } = string.Empty; // Hash نهایی تولید شده توسط Client
    }

    /// <summary>
    /// نتیجه تحلیل اثر انگشت
    /// </summary>
    public class FingerprintAnalysis
    {
        public DateTime Timestamp { get; set; }
        public int RiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public List<string> Flags { get; set; } = new();
        public bool IsDownloader { get; set; }
        public bool IsBot { get; set; }
        public bool IsHeadless { get; set; }
    }

    public enum RiskLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    /// <summary>
    /// رکورد ثبت اثر انگشت
    /// </summary>
    public class FingerprintRecord
    {
        public BrowserFingerprint Fingerprint { get; set; } = new();
        public DateTime RecordedAt { get; set; }
        public string Hash { get; set; } = string.Empty;
    }

    #endregion
}
