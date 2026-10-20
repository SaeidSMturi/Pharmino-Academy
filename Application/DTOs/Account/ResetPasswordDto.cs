using Application.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Account
{
    public class ResetPasswordDto
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "New Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [DataType(DataType.Password)]
        [StrongPassword]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Confirm Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Password and confirmation do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
