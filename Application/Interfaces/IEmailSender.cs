namespace Application.Interfaces
{
    /// <summary>
    /// Sends transactional email (password resets, notifications) through the configured SMTP server.
    /// Settings are read from the "Smtp" section of appsettings.json.
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// Sends one email. Returns true on success, false on failure (the failure is logged, never thrown),
        /// so callers such as the forgot-password flow can respond identically either way.
        /// </summary>
        Task<bool> SendAsync(string toEmail, string subject, string htmlBody, string? textBody = null, CancellationToken cancellationToken = default);
    }
}
