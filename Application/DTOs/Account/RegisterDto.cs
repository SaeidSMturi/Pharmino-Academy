using Application.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Account
{
    public class RegisterDto
    {
        [Display(Name = "First Name")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        [Required(ErrorMessage = "Please enter {0}")]
        [EmailAddress(ErrorMessage = "The entered email is not valid")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Phone Number")]
        [Required(ErrorMessage = "Please enter {0}")]
        [Phone(ErrorMessage = "The entered phone number is not valid")]
        [MaxLength(13, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Password")]
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
