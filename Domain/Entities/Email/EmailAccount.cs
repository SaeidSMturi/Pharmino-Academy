using System.Diagnostics.CodeAnalysis;

namespace Domain.Entities.Email
{
    public class EmailAccount
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int SmtpPortIncoming { get; set; }
        public int SmtpPortOutgoing { get; set; }
        public string SmtpServerIncoming { get; set; } = string.Empty;
        public string SmtpServerOutgoing { get; set; } = string.Empty;
        [AllowNull]
        public bool? IsConnectionSuccessfully { get; set; }
    }
}
