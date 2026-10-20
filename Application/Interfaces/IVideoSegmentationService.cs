namespace Application.Interfaces
{
    public interface IVideoSegmentationService
    {
        string GenerateSegmentToken(int videoId, int userId, int segmentIndex, DateTime expiration);
        bool ValidateSegmentToken(string token, int videoId, int userId, int segmentIndex);
        string GenerateMasterPlaylist(int videoId, int userId, string baseUrl);
        (bool isValid, int segmentIndex) ParseSegmentToken(string token);
    }
}
