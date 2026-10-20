namespace Application.Interfaces
{
    /// <summary>
    /// خروجی ارزیابی سناریوی «سفر غیرممکن» برای یک تلاش ورود
    /// Result of evaluating a single login attempt against the user's last known login location.
    /// </summary>
    public record ImpossibleTravelResult(
        bool IsSuspicious,
        double DistanceKm,
        double ElapsedHours,
        double RequiredSpeedKmH,
        string? PreviousIpAddress,
        int RevokedSessionCount);

    /// <summary>
    /// لایه منطق تشخیص «سفر غیرممکن» - محاسبه فاصله جغرافیایی و سرعت لازم بین دو ورود متوالی از IP های متفاوت
    /// </summary>
    public interface IImpossibleTravelDetectionService
    {
        /// <summary>
        /// ارزیابی یک ورود جدید نسبت به آخرین موقعیت شناخته‌شده کاربر و اعمال اقدامات امنیتی لازم در صورت مشکوک بودن
        /// </summary>
        Task<ImpossibleTravelResult> EvaluateLoginAsync(int userId, string newSessionId, string ipAddress);
    }
}
