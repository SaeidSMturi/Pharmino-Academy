namespace Application.Interfaces
{
    public interface IIpBindingService
    {
        string BindTokenToIp(string token, string ipAddress);
        bool ValidateIpBinding(string boundToken, string currentIpAddress);
        void RecordIpChange(int userId, string oldIp, string newIp);
        bool IsSuspiciousIpPattern(int userId, string ipAddress);
    }

    public class IpChangeRecord
    {
        public string OldIp { get; set; } = string.Empty;
        public string NewIp { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }
}
