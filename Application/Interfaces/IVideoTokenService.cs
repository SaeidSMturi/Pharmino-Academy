namespace Application.Interfaces
{
    public interface IVideoTokenService
    {
        string GenerateToken(int videoId, int userId, int validMinutes = 60);
        bool ValidateToken(string token, int videoId, int userId);
        string GenerateSessionId(int videoId, int userId);
        bool ValidateSessionId(string sessionId, int videoId, int userId);

        // 🔒 One-Time Token System
        string GenerateOneTimeStreamToken(int videoId, int userId, string sessionId);
        (bool isValid, string error) ValidateAndConsumeOneTimeToken(string token, int videoId, int userId, string sessionId);

        // 🔒 Request Signature
        string GenerateRequestSignature(string sessionId, long timestamp, long rangeStart, long rangeEnd);
        bool ValidateRequestSignature(string signature, string sessionId, long timestamp, long rangeStart, long rangeEnd);
    }
}
