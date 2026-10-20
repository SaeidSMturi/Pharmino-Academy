using Application.DTOs.User;

namespace Application.Interfaces
{
    public interface IUserService
    {
        Task<(bool Success, string Message)> EditProfileAsync(int userId, EditProfileDto model);
        Task<(bool Success, string Message)> ChangePasswordAsync(int userId, ChangePasswordDto model);
        Task<EditProfileDto?> GetUserProfileAsync(int userId);
    }
}
