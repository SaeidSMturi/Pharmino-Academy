using Domain.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace Application.Attributes
{
    public class StrongPasswordAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult("Password is required");
            }

            var password = value.ToString()!;

            // Get PasswordPolicyOptions from DI
            var options = validationContext.GetService<IOptions<PasswordPolicyOptions>>();
            if (options == null)
            {
                return new ValidationResult("Password validation configuration not found");
            }

            var policy = options.Value;
            var errors = new List<string>();

            // Check minimum length
            if (password.Length < policy.RequiredLength)
            {
                errors.Add($"at least {policy.RequiredLength} characters");
            }

            // Check uppercase requirement
            if (policy.RequireUppercase && !password.Any(char.IsUpper))
            {
                errors.Add("one uppercase letter");
            }

            // Check lowercase requirement
            if (policy.RequireLowercase && !password.Any(char.IsLower))
            {
                errors.Add("one lowercase letter");
            }

            // Check digit requirement
            if (policy.RequireDigit && !password.Any(char.IsDigit))
            {
                errors.Add("one digit");
            }

            // Check special character requirement
            if (policy.RequireSpecialCharacter)
            {
                if (!password.Any(c => policy.SpecialCharacters.Contains(c)))
                {
                    errors.Add($"one special character ({policy.SpecialCharacters})");
                }
            }

            // Check unique characters
            if (policy.MinimumUniqueCharacters > 0)
            {
                var uniqueChars = password.Distinct().Count();
                if (uniqueChars < policy.MinimumUniqueCharacters)
                {
                    errors.Add($"at least {policy.MinimumUniqueCharacters} unique characters");
                }
            }

            if (errors.Any())
            {
                return new ValidationResult($"Password must contain: {string.Join(", ", errors)}");
            }

            return ValidationResult.Success;
        }
    }
}
