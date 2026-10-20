using Application.DTOs.Account;

namespace Application.Interfaces
{
    public interface IAccountService
    {
        Task<(bool Success, string Message, int? UserId)> RegisterAsync(RegisterDto model);
        Task<(bool Success, string Message, int? UserId)> LoginAsync(LoginDto model);
        Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto model, string resetPageUrl);
        Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto model);
    }
}
