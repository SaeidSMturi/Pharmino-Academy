using System.Text.RegularExpressions;
using Application.Interfaces;

namespace Infrastructure.Services
{
    public class UserAgentParserService : IUserAgentParserService
    {
        // ترتیب بررسی مرورگرها اهمیت دارد: Edge/Opera باید پیش از Chrome بررسی شوند چون UA آن‌ها شامل "Chrome" نیز هست
        private static readonly (string Token, string DisplayName)[] BrowserSignatures =
        {
            ("Edg/", "Microsoft Edge"),
            ("OPR/", "Opera"),
            ("Brave/", "Brave"),
            ("SamsungBrowser/", "Samsung Internet"),
            ("Firefox/", "Firefox"),
            ("Chrome/", "Chrome"),
            ("CriOS/", "Chrome (iOS)"),
            ("FxiOS/", "Firefox (iOS)"),
            ("Safari/", "Safari"),
            ("MSIE ", "Internet Explorer"),
            ("Trident/", "Internet Explorer"),
        };

        private static readonly (Regex Pattern, string DisplayName)[] OsSignatures =
        {
            (new Regex(@"Windows NT 10\.0", RegexOptions.Compiled), "Windows 10/11"),
            (new Regex(@"Windows NT 6\.3", RegexOptions.Compiled), "Windows 8.1"),
            (new Regex(@"Windows NT 6\.2", RegexOptions.Compiled), "Windows 8"),
            (new Regex(@"Windows NT 6\.1", RegexOptions.Compiled), "Windows 7"),
            (new Regex(@"Windows", RegexOptions.Compiled), "Windows"),
            (new Regex(@"iPhone|iPad|iPod", RegexOptions.Compiled), "iOS"),
            (new Regex(@"Mac OS X", RegexOptions.Compiled), "macOS"),
            (new Regex(@"Android", RegexOptions.Compiled), "Android"),
            (new Regex(@"Linux", RegexOptions.Compiled), "Linux"),
        };

        public (string OperatingSystem, string Browser) Parse(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return ("Unknown Device", "Unknown Browser");
            }

            var os = OsSignatures.FirstOrDefault(s => s.Pattern.IsMatch(userAgent)).DisplayName ?? "Unknown OS";
            var browser = BrowserSignatures.FirstOrDefault(s => userAgent.Contains(s.Token, StringComparison.OrdinalIgnoreCase)).DisplayName ?? "Unknown Browser";

            return (os, browser);
        }
    }
}
