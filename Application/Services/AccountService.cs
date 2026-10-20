using Application.DTOs.Account;
using Application.Interfaces;
using Domain.Entities.User;
using Domain.Interfaces;

namespace Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailSender _emailSender;

        public AccountService(IUserRepository userRepository, IPasswordHasher passwordHasher, IEmailSender emailSender)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _emailSender = emailSender;
        }

        public async Task<(bool Success, string Message, int? UserId)> RegisterAsync(RegisterDto model)
        {
            // Username is the Email; ensure email is unique
            if (await _userRepository.IsEmailExistsAsync(model.Email))
            {
                return (false, "This email is already registered", null);
            }

            // Check if phone exists
            if (await _userRepository.IsPhoneNumberExistsAsync(model.PhoneNumber))
            {
                return (false, "This phone number is already registered", null);
            }

            // Create new user - UserName must be the Email
            var user = new User
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Password = _passwordHasher.HashPassword(model.Password),
                ActiveCode = Guid.NewGuid().ToString().Replace("-", ""),
                SecurityCode = Guid.NewGuid().ToString().Replace("-", ""),
                ActivePhoneNumberCode = new Random().Next(10000, 99999).ToString(),
                IsEmailActive = false,
                IsPhoneNumberActive = false,
                RegisterDate = DateTime.UtcNow,
                LastChange = DateTime.UtcNow,
                IsDelete = false,
                IsBan = false,
                TryCount = 0
            };

            var createdUser = await _userRepository.AddUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return (true, "Registration completed successfully", createdUser.UserId);
        }

        public async Task<(bool Success, string Message, int? UserId)> LoginAsync(LoginDto model)
        {
            // Find user by username or email (UserName equals Email in our model)
            var user = await _userRepository.GetUserByUsernameOrEmailAsync(model.UsernameOrEmail);

            if (user == null)
            {
                return (false, "Username or password is incorrect", null);
            }

            // Check if user is banned
            if (user.IsBan)
            {
                return (false, "Your account has been banned", null);
            }

            // Check if user is temporarily locked out
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var remainingMinutes = (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                return (false, $"Your account is temporarily locked due to multiple failed login attempts. Please try again in {remainingMinutes} minute(s).", null);
            }

            // Verify password
            if (!_passwordHasher.VerifyPassword(user.Password, model.Password))
            {
                // Increment try count
                user.TryCount++;
                user.LastTry = DateTime.UtcNow;

                // Temporarily lock out user for 15 minutes if try count reaches 5
                if (user.TryCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    user.TryCount = 0;
                }

                await _userRepository.UpdateUserAsync(user);
                await _userRepository.SaveChangesAsync();

                return (false, "Username or password is incorrect", null);
            }

            // Reset try count, lockout, and update last login
            user.TryCount = 0;
            user.LockoutEnd = null;
            user.LastLoginDate = DateTime.UtcNow;

            // Automatically upgrade legacy SHA-256 password hashes to PBKDF2 with salt
            if (_passwordHasher.NeedsRehash(user.Password))
            {
                user.Password = _passwordHasher.HashPassword(model.Password);
            }

            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return (true, "Login successful", user.UserId);
        }

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(ForgotPasswordDto model, string resetPageUrl)
        {
            // The same message is returned in every case, so the response never reveals whether an
            // account exists, and the reset code is only ever delivered by email, never to the browser.
            const string genericMessage = "If your email is registered in the system, a password recovery link will be sent to you.";

            var cleanEmail = model.Email?.Trim();
            if (string.IsNullOrWhiteSpace(cleanEmail))
            {
                return (true, genericMessage);
            }

            var user = await _userRepository.GetUserByEmailAsync(cleanEmail);
            if (user == null)
            {
                return (true, genericMessage);
            }

            // Generate new security code and set 15-minute expiration
            user.SecurityCode = Guid.NewGuid().ToString().Replace("-", "");
            user.SecurityCodeExpiry = DateTime.UtcNow.AddMinutes(15);
            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            var toEmail = user.Email ?? cleanEmail;
            var resetLink = $"{resetPageUrl}?email={Uri.EscapeDataString(toEmail)}&code={Uri.EscapeDataString(user.SecurityCode)}";
            var plainName = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName;
            var name = System.Net.WebUtility.HtmlEncode(plainName);
            var safeLink = System.Net.WebUtility.HtmlEncode(resetLink);

            var html = $@"<!doctype html>
<html><body style=""font-family:Arial,Helvetica,sans-serif;color:#222;line-height:1.5"">
<p>Hi {name},</p>
<p>We received a request to reset the password for your Pharmino Academy account.</p>
<p><a href=""{safeLink}"" style=""display:inline-block;padding:10px 18px;background:#1a73e8;color:#fff;text-decoration:none;border-radius:4px"">Reset your password</a></p>
<p>If the button doesn't work, copy this link into your browser:<br><span style=""word-break:break-all"">{safeLink}</span></p>
<p>If you didn't request this, you can ignore this email; your password won't change.</p>
<p>Pharmino Academy</p>
</body></html>";

            var text = $"Hi {plainName},\n\nWe received a request to reset the password for your Pharmino Academy account.\n\nReset it here:\n{resetLink}\n\nIf you didn't request this, you can ignore this email; your password won't change.\n\nPharmino Academy";

            // Failures are logged inside the sender; the user still sees the generic message.
            await _emailSender.SendAsync(toEmail, "Reset your Pharmino Academy password", html, text);

            return (true, genericMessage);
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto model)
        {
            var cleanEmail = model.Email?.Trim();
            if (string.IsNullOrWhiteSpace(cleanEmail))
            {
                return (false, "Recovery link is invalid or has expired");
            }

            var user = await _userRepository.GetUserByEmailAsync(cleanEmail);

            if (user == null || user.SecurityCode != model.Code || (user.SecurityCodeExpiry.HasValue && user.SecurityCodeExpiry.Value < DateTime.UtcNow))
            {
                return (false, "Recovery link is invalid or has expired");
            }

            // Update password and security code
            user.Password = _passwordHasher.HashPassword(model.Password);
            user.SecurityCode = Guid.NewGuid().ToString().Replace("-", "");
            user.SecurityCodeExpiry = null;
            user.TryCount = 0;
            user.LockoutEnd = null;
            user.IsBan = false;
            await _userRepository.UpdateUserAsync(user);
            await _userRepository.SaveChangesAsync();

            return (true, "Your password has been changed successfully");
        }
    }
}
