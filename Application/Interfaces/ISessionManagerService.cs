using Application.DTOs.User;

namespace Application.Interfaces
{
    public interface ISessionManagerService
    {
        Task<IReadOnlyList<string>> CreateSessionAsync(SessionRecord session, int maxConcurrentLogins);
        Task<SessionRecord?> GetSessionAsync(string sessionId);
        Task<bool> ValidateAndTouchSessionAsync(string sessionId, TimeSpan slidingExpiration);
        Task RevokeSessionAsync(int userId, string sessionId);
        Task<int> RevokeAllSessionsAsync(int userId, string? exceptSessionId = null);
        Task<List<SessionRecord>> GetActiveSessionsAsync(int userId);
    }
}
