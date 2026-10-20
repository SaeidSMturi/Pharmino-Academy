using Domain.Configuration;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    /// <summary>
    /// رویدادهای سفارشی کوکی احراز هویت - اجرای مدیریت نشست SQL Server و قفل IP/Fingerprint در هر درخواست
    /// </summary>
    public class SessionValidationCookieEvents : CookieAuthenticationEvents
    {
        private readonly ISessionManagerService _sessionManagerService;
        private readonly IDeviceFingerprintService _deviceFingerprintService;
        private readonly SessionSecurityOptions _options;
        private readonly ILogger<SessionValidationCookieEvents> _logger;

        public SessionValidationCookieEvents(
            ISessionManagerService sessionManagerService,
            IDeviceFingerprintService deviceFingerprintService,
            IOptions<SessionSecurityOptions> options,
            ILogger<SessionValidationCookieEvents> logger)
        {
            _sessionManagerService = sessionManagerService;
            _deviceFingerprintService = deviceFingerprintService;
            _options = options.Value;
            _logger = logger;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            var sessionId = principal?.FindFirst("SessionId")?.Value;

            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("🚫 Authentication cookie is missing the SessionId claim - rejecting principal.");
                await RejectAndSignOutAsync(context);
                return;
            }

            var session = await _sessionManagerService.GetSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("🚫 Session {SessionId} was not found in SQL Server (revoked/expired) - rejecting principal.", sessionId);
                await RejectAndSignOutAsync(context);
                return;
            }

            if (_options.EnableStrictIpLocking)
            {
                var currentIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var currentUserAgent = context.HttpContext.Request.Headers.UserAgent.ToString();
                var currentFingerprint = _deviceFingerprintService.GenerateFingerprint(currentUserAgent, currentIp);

                var ipChanged = !string.Equals(session.IpAddress, currentIp, StringComparison.Ordinal);
                var fingerprintChanged = !string.Equals(session.DeviceFingerprint, currentFingerprint, StringComparison.Ordinal);

                if (ipChanged || fingerprintChanged)
                {
                    _logger.LogCritical(
                        "🚫 SECURITY: Session {SessionId} for UserId {UserId} failed IP/Fingerprint binding check. " +
                        "BoundIp={BoundIp} CurrentIp={CurrentIp} IpChanged={IpChanged} FingerprintChanged={FingerprintChanged}.",
                        sessionId, session.UserId, session.IpAddress, currentIp, ipChanged, fingerprintChanged);

                    // ابطال فوری نشست تا SessionId افشا/سرقت‌شده دیگر قابل استفاده نباشد
                    // Immediately revoke so a leaked/stolen SessionId can never be replayed successfully again.
                    await _sessionManagerService.RevokeSessionAsync(session.UserId, sessionId);
                    await RejectAndSignOutAsync(context);
                    return;
                }
            }

            var slidingExpiration = TimeSpan.FromHours(_options.SessionExpirationHours);
            var stillValid = await _sessionManagerService.ValidateAndTouchSessionAsync(sessionId, slidingExpiration);

            if (!stillValid)
            {
                _logger.LogInformation("Session {SessionId} expired during validation - rejecting principal.", sessionId);
                await RejectAndSignOutAsync(context);
            }
        }

        private static async Task RejectAndSignOutAsync(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
