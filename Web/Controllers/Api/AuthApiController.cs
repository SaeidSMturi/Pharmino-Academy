using Application.DTOs.Account;
using Application.Interfaces;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthApiController> _logger;

        public AuthApiController(
            IAccountService accountService,
            IUserRepository userRepository,
            ILogger<AuthApiController> logger)
        {
            _accountService = accountService;
            _userRepository = userRepository;
            _logger = logger;
        }

        public class MobileLoginRequest
        {
            public string? UserName { get; set; }
            public string? UsernameOrEmail { get; set; }
            public string Password { get; set; } = string.Empty;
        }

        public class MobileRegisterRequest
        {
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] MobileLoginRequest request)
        {
            try
            {
                var identifier = !string.IsNullOrWhiteSpace(request.UsernameOrEmail)
                    ? request.UsernameOrEmail
                    : request.UserName ?? string.Empty;

                if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Username/Email and Password are required." });
                }

                var loginDto = new LoginDto
                {
                    UsernameOrEmail = identifier.Trim(),
                    Password = request.Password,
                    RememberMe = true
                };

                var result = await _accountService.LoginAsync(loginDto);
                if (!result.Success || !result.UserId.HasValue)
                {
                    return Unauthorized(new { message = result.Message ?? "Invalid credentials" });
                }

                var user = await _userRepository.GetUserByIdAsync(result.UserId.Value);
                if (user == null)
                {
                    return Unauthorized(new { message = "User not found" });
                }

                return Ok(new
                {
                    userId = user.UserId,
                    fullName = $"{user.FirstName} {user.LastName}".Trim(),
                    userName = user.UserName ?? user.Email,
                    email = user.Email ?? string.Empty,
                    phoneNumber = user.PhoneNumber,
                    token = Guid.NewGuid().ToString("N") // Session token
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile API Login Error");
                return StatusCode(500, new { message = "An error occurred during login." });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] MobileRegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { message = "Email and Password are required." });
                }

                var names = request.FullName.Split(' ', 2);
                var firstName = names.Length > 0 ? names[0] : request.FullName;
                var lastName = names.Length > 1 ? names[1] : string.Empty;

                var registerDto = new RegisterDto
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = request.Email.Trim(),
                    PhoneNumber = request.PhoneNumber,
                    Password = request.Password,
                    ConfirmPassword = request.Password
                };

                var result = await _accountService.RegisterAsync(registerDto);
                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                return Ok(new
                {
                    message = "Registered successfully. You can now log in.",
                    email = request.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mobile API Register Error");
                return StatusCode(500, new { message = "An error occurred during registration." });
            }
        }
    }
}
