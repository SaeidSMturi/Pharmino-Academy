using Domain.Configuration;
using Microsoft.Extensions.Options;

namespace Application.Services
{
    public interface IPasswordValidator
    {
        (bool IsValid, List<string> Errors) ValidatePassword(string password);
        string GetPasswordRequirementsMessage();
    }

    public class PasswordValidator : IPasswordValidator
    {
        private readonly PasswordPolicyOptions _options;

        public PasswordValidator(IOptions<PasswordPolicyOptions> options)
        {
            _options = options.Value;
        }

        public (bool IsValid, List<string> Errors) ValidatePassword(string password)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(password))
            {
                errors.Add("Password is required");
                return (false, errors);
            }

            // Check minimum length
            if (password.Length < _options.RequiredLength)
            {
                errors.Add($"Password must be at least {_options.RequiredLength} characters long");
            }

            // Check uppercase requirement
            if (_options.RequireUppercase && !password.Any(char.IsUpper))
            {
                errors.Add("Password must contain at least one uppercase letter (A-Z)");
            }

            // Check lowercase requirement
            if (_options.RequireLowercase && !password.Any(char.IsLower))
            {
                errors.Add("Password must contain at least one lowercase letter (a-z)");
            }

            // Check digit requirement
            if (_options.RequireDigit && !password.Any(char.IsDigit))
            {
                errors.Add("Password must contain at least one digit (0-9)");
            }

            // Check special character requirement
            if (_options.RequireSpecialCharacter)
            {
                if (!password.Any(c => _options.SpecialCharacters.Contains(c)))
                {
                    errors.Add($"Password must contain at least one special character ({_options.SpecialCharacters})");
                }
            }

            // Check unique characters
            if (_options.MinimumUniqueCharacters > 0)
            {
                var uniqueChars = password.Distinct().Count();
                if (uniqueChars < _options.MinimumUniqueCharacters)
                {
                    errors.Add($"Password must contain at least {_options.MinimumUniqueCharacters} unique characters");
                }
            }

            return (errors.Count == 0, errors);
        }

        public string GetPasswordRequirementsMessage()
        {
            var requirements = new List<string>();

            requirements.Add($"At least {_options.RequiredLength} characters");

            if (_options.RequireUppercase)
                requirements.Add("One uppercase letter (A-Z)");

            if (_options.RequireLowercase)
                requirements.Add("One lowercase letter (a-z)");

            if (_options.RequireDigit)
                requirements.Add("One digit (0-9)");

            if (_options.RequireSpecialCharacter)
                requirements.Add($"One special character ({_options.SpecialCharacters})");

            if (_options.MinimumUniqueCharacters > 0)
                requirements.Add($"At least {_options.MinimumUniqueCharacters} unique characters");

            return "Password must contain: " + string.Join(", ", requirements);
        }
    }
}
