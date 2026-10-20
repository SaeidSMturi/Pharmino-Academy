using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Account
{
    public class LoginDto
    {
        [Display(Name = "Username or Email")]
        [Required(ErrorMessage = "Please enter {0}")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Display(Name = "Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }
}
