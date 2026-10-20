using Application.Interfaces;
using Application.DTOs.User;
using Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Infrastructure.Persistence;

namespace Infrastructure.Services
{
    public class SqlSessionManagerService : ISessionManagerService
    {
        private readonly WebContext _context;
        private readonly ILogger<SqlSessionManagerService> _logger;

        public SqlSessionManagerService(WebContext context, ILogger<SqlSessionManagerService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> CreateSessionAsync(SessionRecord session, int maxConcurrentLogins)
        {
            var evicted = new List<string>();
            var nowUtc = DateTime.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (maxConcurrentLogins > 0)
            {
                var activeCount = await _context.UserActiveSessions
                    .AsNoTracking()
                    .Where(s => s.UserId == session.UserId && s.ExpiresAtUtc > nowUtc)
                    .CountAsync();

                if (activeCount >= maxConcurrentLogins)
                {
                    var overflowCount = activeCount - maxConcurrentLogins + 1;

                    var staleSessionIds = await _context.UserActiveSessions
                        .AsNoTracking()
                        .Where(s => s.UserId == session.UserId && s.ExpiresAtUtc > nowUtc)
                        .OrderBy(s => s.LastActivityUtc)
                        .Take(overflowCount)
                        .Select(s => s.SessionId)
                        .ToListAsync();

                    if (staleSessionIds.Count > 0)
                    {
                        await _context.UserActiveSessions
                            .Where(s => s.UserId == session.UserId && staleSessionIds.Contains(s.SessionId))
                            .ExecuteDeleteAsync();

                        evicted.AddRange(staleSessionIds);

                        foreach (var staleSessionId in staleSessionIds)
                        {
                            _logger.LogWarning(
                                "🔒 Concurrent login limit ({Max}) reached for UserId {UserId}. Evicted oldest session {SessionId}.",
                                maxConcurrentLogins, session.UserId, staleSessionId);
                        }
                    }
                }
            }

            await _context.UserActiveSessions.AddAsync(new UserActiveSession
            {
                SessionId = session.SessionId,
                UserId = session.UserId,
                IpAddress = session.IpAddress,
                UserAgent = session.UserAgent,
                DeviceFingerprint = session.DeviceFingerprint,
                CreatedAtUtc = session.CreatedAtUtc,
                LastActivityUtc = session.LastActivityUtc,
                ExpiresAtUtc = session.ExpiresAtUtc
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return evicted;
        }

        public async Task<SessionRecord?> GetSessionAsync(string sessionId)
        {
            var entity = await _context.UserActiveSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            return entity == null ? null : ToRecord(entity);
        }

        public async Task<bool> ValidateAndTouchSessionAsync(string sessionId, TimeSpan slidingExpiration)
        {
            var nowUtc = DateTime.UtcNow;
            var newExpiresAtUtc = nowUtc.Add(slidingExpiration);

            var updatedRows = await _context.UserActiveSessions
                .Where(s => s.SessionId == sessionId && s.ExpiresAtUtc > nowUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.LastActivityUtc, nowUtc)
                    .SetProperty(s => s.ExpiresAtUtc, newExpiresAtUtc));

            if (updatedRows > 0)
            {
                return true;
            }

            await _context.UserActiveSessions
                .Where(s => s.SessionId == sessionId && s.ExpiresAtUtc <= nowUtc)
                .ExecuteDeleteAsync();

            return false;
        }

        public async Task RevokeSessionAsync(int userId, string sessionId)
        {
            await _context.UserActiveSessions
                .Where(s => s.UserId == userId && s.SessionId == sessionId)
                .ExecuteDeleteAsync();
        }

        public async Task<int> RevokeAllSessionsAsync(int userId, string? exceptSessionId = null)
        {
            var query = _context.UserActiveSessions.Where(s => s.UserId == userId);

            if (!string.IsNullOrEmpty(exceptSessionId))
            {
                query = query.Where(s => s.SessionId != exceptSessionId);
            }

            return await query.ExecuteDeleteAsync();
        }

        public async Task<List<SessionRecord>> GetActiveSessionsAsync(int userId)
        {
            var nowUtc = DateTime.UtcNow;

            var entities = await _context.UserActiveSessions
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.ExpiresAtUtc > nowUtc)
                .OrderByDescending(s => s.LastActivityUtc)
                .ToListAsync();

            return entities.Select(ToRecord).ToList();
        }

        private static SessionRecord ToRecord(UserActiveSession entity) => new()
        {
            SessionId = entity.SessionId,
            UserId = entity.UserId,
            IpAddress = entity.IpAddress,
            UserAgent = entity.UserAgent,
            DeviceFingerprint = entity.DeviceFingerprint,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastActivityUtc = entity.LastActivityUtc,
            ExpiresAtUtc = entity.ExpiresAtUtc
        };
    }
}
