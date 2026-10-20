using Application.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Web.Areas.Admin.ViewModels
{
    public class CreateUserViewModel
    {
        [Display(Name = "Username")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string UserName { get; set; }

        [Display(Name = "First Name")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string? LastName { get; set; }

        [Display(Name = "Email")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        [EmailAddress(ErrorMessage = "The email entered is not valid")]
        public string Email { get; set; }

        [Display(Name = "Phone Number")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(13, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [StrongPassword]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Confirm Password")]
        [Required(ErrorMessage = "Please enter {0}")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Password and confirmation do not match")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Email is Active")]
        public bool IsEmailActive { get; set; }

        [Display(Name = "Phone Number is Active")]
        public bool IsPhoneNumberActive { get; set; }

        [Display(Name = "Ban User")]
        public bool IsBan { get; set; }
    }
}
