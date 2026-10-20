using Domain.Models;

namespace Application.Interfaces
{
    public interface IVideoSessionManager
    {
        bool CanStartStream(int userId, int videoId);
        void StartStream(int userId, int videoId, string sessionId, string ipAddress, string userAgent);
        void EndStream(string sessionId);
        int GetActiveStreamsCount(int userId);
        VideoStreamSession? GetSession(string sessionId);
        void UpdateHeartbeat(string sessionId, string playerState, double position, double duration, double bufferHealth);
        void RecordChunkRequest(string sessionId, long startByte, long endByte, long bytesTransferred, TimeSpan duration);
        void RecordSeek(string sessionId, double fromPosition, double toPosition);
        int CalculateSuspicionScore(string sessionId);
        bool ShouldBlockStream(string sessionId);
        bool ShouldThrottleStream(string sessionId);
        int GetCurrentBandwidthLimit(string sessionId);
    }
}
