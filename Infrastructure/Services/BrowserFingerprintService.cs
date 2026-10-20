using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class BrowserFingerprintService : IBrowserFingerprintService
    {
        private readonly ILogger<BrowserFingerprintService> _logger;
        private readonly string _secretKey;

        // کش اثر انگشت‌های کاربران
        private readonly ConcurrentDictionary<int, List<FingerprintRecord>> _userFingerprints = new();

        // لیست اثر انگشت‌های مشکوک (Download Managers, Bots, etc.)
        private static readonly HashSet<string> SuspiciousUserAgentPatterns = new(StringComparer.OrdinalIgnoreCase)
        {
            "idm", "internet download manager", "wget", "curl", "aria2",
            "python-requests", "python-urllib", "java/", "apache-httpclient",
            "go-http-client", "node-fetch", "axios", "got/", "request/",
            "libwww-perl", "lwp-", "mechanize", "scrapy", "phantom", "selenium",
            "headless", "puppeteer", "playwright", "webdriver", "chromedriver",
            "bot", "crawler", "spider", "scraper", "downloader"
        };

        // User-Agent های معتبر مرورگرها
        private static readonly string[] ValidBrowserPatterns = new[]
        {
            "Mozilla/5.0", "Chrome/", "Firefox/", "Safari/", "Edge/", "Opera/"
        };

        public BrowserFingerprintService(IConfiguration configuration, ILogger<BrowserFingerprintService> logger)
        {
            _logger = logger;
            _secretKey = configuration["VideoSecurity:SecretKey"]
                ?? throw new ArgumentNullException(nameof(configuration), "VideoSecurity:SecretKey");
        }

        /// <summary>
        /// تولید اثر انگشت سمت سرور از HTTP Headers
        /// </summary>
        public string GenerateServerSideFingerprint(HttpContext context)
        {
            var components = new List<string>
            {
                context.Request.Headers.UserAgent.ToString(),
                context.Request.Headers.AcceptLanguage.ToString(),
                context.Request.Headers.AcceptEncoding.ToString(),
                context.Request.Headers.Accept.ToString(),
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                context.Request.Headers["Sec-Ch-Ua"].ToString(),
                context.Request.Headers["Sec-Ch-Ua-Platform"].ToString(),
                context.Request.Headers["Sec-Ch-Ua-Mobile"].ToString()
            };

            var combined = string.Join("|", components);
            return HashFingerprint(combined);
        }

        /// <summary>
        /// اعتبارسنجی اثر انگشت با tolerance
        /// </summary>
        public bool ValidateFingerprint(string clientFingerprint, string serverFingerprint, int tolerance = 3)
        {
            if (string.IsNullOrEmpty(clientFingerprint) || string.IsNullOrEmpty(serverFingerprint))
                return false;

            // مقایسه exact
            if (clientFingerprint == serverFingerprint)
                return true;

            // مقایسه با tolerance (برای تغییرات جزئی)
            var similarity = CalculateSimilarity(clientFingerprint, serverFingerprint);
            return similarity >= (10 - tolerance);
        }

        /// <summary>
        /// Hash کردن اثر انگشت
        /// </summary>
        public string HashFingerprint(string fingerprint)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));
            return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        /// <summary>
        /// تحلیل اثر انگشت برای شناسایی تهدیدات
        /// </summary>
        public FingerprintAnalysis AnalyzeFingerprint(BrowserFingerprint fingerprint)
        {
            var analysis = new FingerprintAnalysis
            {
                Timestamp = DateTime.UtcNow,
                RiskScore = 0,
                Flags = new List<string>()
            };

            // 1. بررسی User-Agent
            var ua = fingerprint.UserAgent?.ToLower() ?? "";

            if (string.IsNullOrEmpty(ua))
            {
                analysis.RiskScore += 40;
                analysis.Flags.Add("NO_USER_AGENT");
            }
            else
            {
                // بررسی الگوهای مشکوک
                foreach (var pattern in SuspiciousUserAgentPatterns)
                {
                    if (ua.Contains(pattern))
                    {
                        analysis.RiskScore += 50;
                        analysis.Flags.Add($"SUSPICIOUS_UA:{pattern.ToUpper()}");
                        analysis.IsDownloader = true;
                    }
                }

                // بررسی اینکه آیا مرورگر معتبر است
                var hasValidBrowser = ValidBrowserPatterns.Any(p => ua.Contains(p.ToLower()));
                if (!hasValidBrowser)
                {
                    analysis.RiskScore += 30;
                    analysis.Flags.Add("INVALID_BROWSER");
                }
            }

            // 2. بررسی Canvas Fingerprint
            if (string.IsNullOrEmpty(fingerprint.CanvasHash))
            {
                analysis.RiskScore += 25;
                analysis.Flags.Add("NO_CANVAS");
                analysis.IsHeadless = true;
            }

            // 3. بررسی WebGL
            if (string.IsNullOrEmpty(fingerprint.WebGLRenderer))
            {
                analysis.RiskScore += 20;
                analysis.Flags.Add("NO_WEBGL");
                analysis.IsHeadless = true;
            }
            else if (fingerprint.WebGLRenderer.Contains("SwiftShader") ||
                     fingerprint.WebGLRenderer.Contains("Mesa"))
            {
                analysis.RiskScore += 35;
                analysis.Flags.Add("VIRTUAL_GPU");
                analysis.IsHeadless = true;
            }

            // 4. بررسی Screen Resolution
            if (fingerprint.ScreenWidth == 0 || fingerprint.ScreenHeight == 0)
            {
                analysis.RiskScore += 20;
                analysis.Flags.Add("NO_SCREEN");
            }
            else if (fingerprint.ScreenWidth == 800 && fingerprint.ScreenHeight == 600)
            {
                analysis.RiskScore += 15;
                analysis.Flags.Add("SUSPICIOUS_RESOLUTION");
            }

            // 5. بررسی Plugins
            if (fingerprint.PluginCount == 0 && !ua.Contains("mobile"))
            {
                analysis.RiskScore += 15;
                analysis.Flags.Add("NO_PLUGINS");
            }

            // 6. بررسی Timezone
            if (string.IsNullOrEmpty(fingerprint.Timezone))
            {
                analysis.RiskScore += 10;
                analysis.Flags.Add("NO_TIMEZONE");
            }

            // 7. بررسی Language
            if (fingerprint.Languages == null || fingerprint.Languages.Length == 0)
            {
                analysis.RiskScore += 10;
                analysis.Flags.Add("NO_LANGUAGES");
            }

            // 8. بررسی Hardware Concurrency
            if (fingerprint.HardwareConcurrency == 0)
            {
                analysis.RiskScore += 15;
                analysis.Flags.Add("NO_HARDWARE_INFO");
            }
            else if (fingerprint.HardwareConcurrency > 64)
            {
                analysis.RiskScore += 10;
                analysis.Flags.Add("UNUSUAL_CORES");
            }

            // 9. بررسی DevTools
            if (fingerprint.DevToolsOpen)
            {
                analysis.RiskScore += 20;
                analysis.Flags.Add("DEVTOOLS_OPEN");
            }

            // 10. بررسی Automation Flags
            if (fingerprint.HasAutomationFlags)
            {
                analysis.RiskScore += 40;
                analysis.Flags.Add("AUTOMATION_DETECTED");
                analysis.IsBot = true;
            }

            // 11. بررسی Touch Support inconsistency
            if (fingerprint.TouchSupport && !fingerprint.IsMobile)
            {
                analysis.RiskScore += 5;
                analysis.Flags.Add("TOUCH_INCONSISTENCY");
            }

            // 12. بررسی AudioContext
            if (string.IsNullOrEmpty(fingerprint.AudioContextHash))
            {
                analysis.RiskScore += 10;
                analysis.Flags.Add("NO_AUDIO_CONTEXT");
            }

            // تعیین سطح ریسک
            analysis.RiskLevel = analysis.RiskScore switch
            {
                >= 80 => RiskLevel.Critical,
                >= 60 => RiskLevel.High,
                >= 40 => RiskLevel.Medium,
                >= 20 => RiskLevel.Low,
                _ => RiskLevel.None
            };

            return analysis;
        }

        /// <summary>
        /// بررسی اینکه آیا اثر انگشت متعلق به یک Download Manager شناخته‌شده است
        /// </summary>
        public bool IsKnownDownloaderFingerprint(BrowserFingerprint fingerprint)
        {
            var analysis = AnalyzeFingerprint(fingerprint);
            return analysis.IsDownloader || analysis.IsBot || analysis.RiskLevel >= RiskLevel.High;
        }

        /// <summary>
        /// ثبت اثر انگشت برای کاربر
        /// </summary>
        public void RecordFingerprint(int userId, BrowserFingerprint fingerprint)
        {
            var record = new FingerprintRecord
            {
                Fingerprint = fingerprint,
                RecordedAt = DateTime.UtcNow,
                Hash = HashFingerprint(JsonSerializer.Serialize(fingerprint))
            };

            _userFingerprints.AddOrUpdate(userId,
                new List<FingerprintRecord> { record },
                (_, existing) =>
                {
                    existing.Add(record);
                    // نگه‌داری فقط 20 اثر انگشت آخر
                    if (existing.Count > 20)
                        existing.RemoveAt(0);
                    return existing;
                });
        }

        /// <summary>
        /// بررسی anomaly در اثر انگشت
        /// </summary>
        public bool HasFingerprintAnomaly(int userId, BrowserFingerprint currentFingerprint)
        {
            if (!_userFingerprints.TryGetValue(userId, out var history) || history.Count < 3)
                return false;

            var currentHash = HashFingerprint(JsonSerializer.Serialize(currentFingerprint));

            // بررسی تعداد اثر انگشت‌های منحصر به فرد
            var uniqueFingerprints = history.Select(r => r.Hash).Distinct().Count();

            // اگر کاربر بیش از 5 اثر انگشت مختلف در تاریخچه داشته باشد → مشکوک
            if (uniqueFingerprints > 5)
            {
                _logger.LogWarning(
                    "🚨 Fingerprint anomaly detected - UserId: {UserId}, UniqueFingerprints: {Count}",
                    userId, uniqueFingerprints);
                return true;
            }

            // بررسی تغییر ناگهانی اثر انگشت
            var lastFingerprint = history.LastOrDefault();
            if (lastFingerprint != null && lastFingerprint.Hash != currentHash)
            {
                var timeSinceLastChange = DateTime.UtcNow - lastFingerprint.RecordedAt;

                // اگر در کمتر از 1 دقیقه اثر انگشت عوض شده → خیلی مشکوک
                if (timeSinceLastChange.TotalMinutes < 1)
                {
                    _logger.LogWarning(
                        "🚨 Rapid fingerprint change - UserId: {UserId}, TimeSince: {Time}s",
                        userId, timeSinceLastChange.TotalSeconds);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// محاسبه شباهت دو اثر انگشت
        /// </summary>
        private int CalculateSimilarity(string fp1, string fp2)
        {
            if (string.IsNullOrEmpty(fp1) || string.IsNullOrEmpty(fp2))
                return 0;

            var minLen = Math.Min(fp1.Length, fp2.Length);
            var matches = 0;

            for (int i = 0; i < minLen; i++)
            {
                if (fp1[i] == fp2[i])
                    matches++;
            }

            return (int)(((double)matches / minLen) * 10);
        }
    }
}
