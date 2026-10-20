using Application.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.User
{
    public class ChangePasswordDto
    {
        [Display(Name = "Current Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Display(Name = "New Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [DataType(DataType.Password)]
        [StrongPassword]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "Confirm New Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation do not match")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
