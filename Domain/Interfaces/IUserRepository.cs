using Domain.Entities.User;

namespace Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetUserByIdAsync(int userId);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByUsernameOrEmailAsync(string usernameOrEmail);
        Task<bool> IsUsernameExistsAsync(string username);
        Task<bool> IsEmailExistsAsync(string email);
        Task<bool> IsPhoneNumberExistsAsync(string phoneNumber);
        Task<User> AddUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task<bool> SaveChangesAsync();
    }
}
