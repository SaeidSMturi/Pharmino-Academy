using Domain.Configuration;
using Application.Interfaces;
using Infrastructure.Persistence;
using Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ImpossibleTravelDetectionService : IImpossibleTravelDetectionService
    {
        private readonly WebContext _context;
        private readonly IGeoIpService _geoIpService;
        private readonly ISuspiciousActivityService _suspiciousActivityService;
        private readonly ISessionManagerService _sessionManagerService;
        private readonly SessionSecurityOptions _options;
        private readonly ILogger<ImpossibleTravelDetectionService> _logger;

        private const double EarthRadiusKm = 6371.0;

        public ImpossibleTravelDetectionService(
            WebContext context,
            IGeoIpService geoIpService,
            ISuspiciousActivityService suspiciousActivityService,
            ISessionManagerService sessionManagerService,
            IOptions<SessionSecurityOptions> options,
            ILogger<ImpossibleTravelDetectionService> logger)
        {
            _context = context;
            _geoIpService = geoIpService;
            _suspiciousActivityService = suspiciousActivityService;
            _sessionManagerService = sessionManagerService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ImpossibleTravelResult> EvaluateLoginAsync(int userId, string newSessionId, string ipAddress)
        {
            var previous = await _context.UserLastKnownLocations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.UserId == userId);

            var result = new ImpossibleTravelResult(false, 0, 0, 0, previous?.IpAddress, 0);
            var nowUtc = DateTime.UtcNow;

            // اسنپ‌شات‌های قدیمی‌تر از دوره نگهداری برای این ارزیابی نادیده گرفته می‌شوند (معادل انقضای TTL در Redis)
            // Snapshots older than the retention window are ignored for evaluation purposes (mirrors Redis TTL expiry).
            var retentionCutoffUtc = nowUtc.AddDays(-_options.LastKnownLocationRetentionDays);
            var isPreviousWithinRetention = previous != null && previous.TimestampUtc >= retentionCutoffUtc;

            if (isPreviousWithinRetention && !string.Equals(previous!.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase))
            {
                var currentLocation = await _geoIpService.GetLocationAsync(ipAddress);
                var distanceKm = CalculateHaversineDistanceKm(
                    previous.Latitude, previous.Longitude,
                    currentLocation.Latitude, currentLocation.Longitude);

                var elapsed = nowUtc - previous.TimestampUtc;
                // حداقل 1 ثانیه در نظر گرفته می‌شود تا از تقسیم بر صفر برای ورودهای تقریباً هم‌زمان جلوگیری شود
                // Floor elapsed time at 1 second to avoid a divide-by-zero for near-simultaneous logins.
                var elapsedHours = Math.Max(elapsed.TotalHours, 1.0 / 3600.0);
                var requiredSpeedKmH = distanceKm / elapsedHours;

                var isSuspicious = distanceKm >= _options.MinimumDistanceKmForEvaluation
                    && requiredSpeedKmH > _options.ImpossibleTravelSpeedThresholdKmH;

                var revokedCount = 0;

                if (isSuspicious)
                {
                    _logger.LogCritical(
                        "🚨 IMPOSSIBLE TRAVEL detected for UserId {UserId}: {DistanceKm:N0} km in {ElapsedHours:N2}h " +
                        "(required speed {RequiredSpeed:N0} km/h, threshold {Threshold:N0} km/h). Previous IP: {PreviousIp}, New IP: {NewIp}.",
                        userId, distanceKm, elapsedHours, requiredSpeedKmH, _options.ImpossibleTravelSpeedThresholdKmH,
                        previous.IpAddress, ipAddress);

                    await _suspiciousActivityService.LogSuspiciousActivityAsync(
                        userId,
                        activityType: "ImpossibleTravel",
                        suspicionScore: 90,
                        reason: $"Login implies {requiredSpeedKmH:N0} km/h travel ({distanceKm:N0} km in {elapsedHours:N2}h), exceeding the {_options.ImpossibleTravelSpeedThresholdKmH:N0} km/h threshold.",
                        ipAddress: ipAddress,
                        additionalData: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            PreviousIp = previous.IpAddress,
                            NewIp = ipAddress,
                            DistanceKm = distanceKm,
                            ElapsedHours = elapsedHours,
                            RequiredSpeedKmH = requiredSpeedKmH
                        }));

                    // باطل کردن تمام نشست‌های قدیمی به جز نشست جدیدی که همین الان احراز هویت شده است
                    // Force-logout every older session, keeping only the brand-new (just-authenticated) one.
                    revokedCount = await _sessionManagerService.RevokeAllSessionsAsync(userId, exceptSessionId: newSessionId);
                }

                result = new ImpossibleTravelResult(isSuspicious, distanceKm, elapsedHours, requiredSpeedKmH, previous.IpAddress, revokedCount);
            }

            var newLocation = isPreviousWithinRetention && string.Equals(previous!.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase)
                ? new GeoLocation(previous.Latitude, previous.Longitude, string.Empty, string.Empty)
                : await _geoIpService.GetLocationAsync(ipAddress);

            await UpsertLastKnownLocationAsync(userId, ipAddress, newLocation, nowUtc);

            return result;
        }

        private async Task UpsertLastKnownLocationAsync(int userId, string ipAddress, GeoLocation location, DateTime nowUtc)
        {
            var updatedRows = await _context.UserLastKnownLocations
                .Where(l => l.UserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(l => l.IpAddress, ipAddress)
                    .SetProperty(l => l.Latitude, location.Latitude)
                    .SetProperty(l => l.Longitude, location.Longitude)
                    .SetProperty(l => l.TimestampUtc, nowUtc));

            if (updatedRows == 0)
            {
                _context.UserLastKnownLocations.Add(new UserLastKnownLocation
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    TimestampUtc = nowUtc
                });

                await _context.SaveChangesAsync();
            }
        }

        private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                    * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
