using System.Net;
using System.Net.Mail;
using System.Text;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// SMTP email sender built on System.Net.Mail (no extra NuGet package needed).
    /// Reads the "Smtp" section of appsettings.json:
    ///   Host, Port, UseStartTls, Username, Password, FromEmail, FromName
    /// Port 587 + UseStartTls=true is the standard authenticated submission setup.
    /// </summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default)
        {
            var smtp = _configuration.GetSection("Smtp");
            var host = smtp["Host"];
            var username = smtp["Username"];
            var password = smtp["Password"];
            var fromEmail = smtp["FromEmail"] ?? username;
            var fromName = smtp["FromName"] ?? "Pharmino Academy";
            var port = smtp.GetValue<int?>("Port") ?? 587;
            var useStartTls = smtp.GetValue<bool?>("UseStartTls") ?? true;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogError("Email not sent to {To}: the Smtp section in appsettings.json is missing Host or FromEmail.", toEmail);
                return false;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName, Encoding.UTF8),
                    Subject = subject,
                    SubjectEncoding = Encoding.UTF8,
                    BodyEncoding = Encoding.UTF8,
                };
                message.To.Add(new MailAddress(toEmail));

                // Plain-text part first, HTML second: clients show the last alternative they support.
                if (!string.IsNullOrWhiteSpace(textBody))
                {
                    message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain"));
                }
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html"));

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = useStartTls,               // on port 587 this means STARTTLS
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = string.IsNullOrWhiteSpace(username) ? null : new NetworkCredential(username, password),
                    Timeout = 20000,
                };

                await client.SendMailAsync(message, cancellationToken);
                _logger.LogInformation("Email sent to {To} with subject {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To} via {Host}:{Port}", toEmail, host, port);
                return false;
            }
        }
    }
}
