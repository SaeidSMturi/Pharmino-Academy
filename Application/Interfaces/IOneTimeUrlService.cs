namespace Application.Interfaces
{
    public interface IOneTimeUrlService
    {
        string GenerateOneTimeUrl(int videoId, int userId, string sessionId, int ttlSeconds = 30);
        bool ValidateAndConsumeUrl(string urlToken, int videoId, int userId, string sessionId);
        void CleanExpiredUrls();
    }

    public class OneTimeUrlData
    {
        public string UrlId { get; set; } = string.Empty;
        public int VideoId { get; set; }
        public int UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsConsumed { get; set; }
        public DateTime? ConsumedAt { get; set; }
    }
}
