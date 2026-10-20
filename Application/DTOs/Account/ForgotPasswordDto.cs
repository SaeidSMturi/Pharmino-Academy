using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Account
{
    public class ForgotPasswordDto
    {
        [Display(Name = "Email")]
        [Required(ErrorMessage = "Please enter {0}")]
        [EmailAddress(ErrorMessage = "The entered email is not valid")]
        public string Email { get; set; } = string.Empty;
    }
}
