using Application.DTOs.User;
using Application.Interfaces;
using Domain.Interfaces;

namespace Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;

        public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<EditProfileDto?> GetUserProfileAsync(int userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user == null)
            {
                return null;
            }

            return new EditProfileDto
            {
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                // Username is the Email in our system now
                UserName = user.Email ?? user.UserName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                LoginWithGoogle = user.LoginWithGoogle
            };
        }

        public async Task<(bool Success, string Message)> EditProfileAsync(int userId, EditProfileDto model)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user == null)
            {
                return (false, "User not found");
            }

            // Email must be unique
            if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUser = await _userRepository.GetUserByEmailAsync(model.Email);
                if (existingUser != null && existingUser.UserId != userId)
                {
                    return (false, "This email is already registered");
                }
            }

            // Update user
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;
            user.UserName = model.Email; // keep username equal to email
            user.PhoneNumber = model.PhoneNumber;
            user.LastChange = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return (true, "Profile updated successfully");
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, ChangePasswordDto model)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user == null)
            {
                return (false, "User not found");
            }

            // Verify current password
            if (!_passwordHasher.VerifyPassword(user.Password, model.CurrentPassword))
            {
                return (false, "Current password is incorrect");
            }

            // Update password
            user.Password = _passwordHasher.HashPassword(model.NewPassword);
            user.LastChange = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return (true, "Password changed successfully");
        }
    }
}
