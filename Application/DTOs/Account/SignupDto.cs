using Application.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Account
{
    /// <summary>The terms the sign-up page shows. Bump the version whenever the wording changes.</summary>
    public static class SignupTerms
    {
        public const string Version = "1.0";
        public const int MinimumAge = 18;
    }

    /// <summary>
    /// Everything the one-page sign-up collects, posted once, after the email has been verified.
    /// The server re-checks all of it; nothing the page validated is trusted on its own.
    /// </summary>
    public class SignupDto
    {
        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>yyyy-MM-dd</summary>
        [Required, RegularExpression(@"^\d{4}-\d{2}-\d{2}$")]
        public string DateOfBirth { get; set; } = string.Empty;

        [Required, StrongPassword, MaxLength(200)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare(nameof(Password), ErrorMessage = "Password and confirmation do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>As typed; normalised to E.164 on the server.</summary>
        [Required, MaxLength(40)]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>ISO 3166-1 alpha-2 country the number was typed for, e.g. "OM".</summary>
        [Required, RegularExpression("^[A-Za-z]{2}$")]
        public string PhoneRegion { get; set; } = string.Empty;

        public bool AcceptTerms { get; set; }
        public bool AcceptCompensation { get; set; }
        public bool AcceptDataProcessing { get; set; }
        public bool MarketingConsent { get; set; }

        /// <summary>The full name typed to sign the terms.</summary>
        [Required, MaxLength(400)]
        public string Signature { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string TermsVersion { get; set; } = string.Empty;

        /// <summary>Proof the email was verified, issued by the verify-code step.</summary>
        [Required]
        public string VerificationToken { get; set; } = string.Empty;
    }
}
