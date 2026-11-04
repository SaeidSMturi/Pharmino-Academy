using Application.DTOs.Account;

namespace Application.Interfaces
{
    public interface IAccountService
    {
        Task<(bool Success, string Message, int? UserId)> RegisterAsync(RegisterDto model);

        /// <summary>
        /// Creates the account at the end of the one-page sign-up. The caller has already verified the email
        /// and validated the fields; this re-checks uniqueness and stores the terms-acceptance record.
        /// </summary>
        Task<(bool Success, string Message, int? UserId)> CreateVerifiedAccountAsync(SignupDto model, string phoneE164, DateTime dateOfBirth, string ipAddress);
        Task<(bool Success, string Message, int? UserId)> LoginAsync(LoginDto model);
        Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto model, string resetPageUrl);
        Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto model);
    }
}
