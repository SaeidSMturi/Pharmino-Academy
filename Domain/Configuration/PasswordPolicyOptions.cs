namespace Domain.Configuration
{
    public class PasswordPolicyOptions
    {
        public const string SectionName = "PasswordPolicy";

        /// <summary>
        /// Minimum required length of password
        /// </summary>
        public int RequiredLength { get; set; } = 8;

        /// <summary>
        /// Require at least one uppercase letter (A-Z)
        /// </summary>
        public bool RequireUppercase { get; set; } = true;

        /// <summary>
        /// Require at least one lowercase letter (a-z)
        /// </summary>
        public bool RequireLowercase { get; set; } = true;

        /// <summary>
        /// Require at least one digit (0-9)
        /// </summary>
        public bool RequireDigit { get; set; } = true;

        /// <summary>
        /// Require at least one special character
        /// </summary>
        public bool RequireSpecialCharacter { get; set; } = true;

        /// <summary>
        /// List of allowed special characters
        /// </summary>
        public string SpecialCharacters { get; set; } = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        /// <summary>
        /// Minimum number of unique characters required
        /// </summary>
        public int MinimumUniqueCharacters { get; set; } = 4;
    }
}
