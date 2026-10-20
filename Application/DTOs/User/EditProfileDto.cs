using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.User
{
    public class EditProfileDto
    {
        [Display(Name = "First Name")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Username")]
        [Required(ErrorMessage = "Please enter {0}")]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string UserName { get; set; } = string.Empty;

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

        public bool LoginWithGoogle { get; set; }
    }
}
