using Application.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Web.Areas.Admin.ViewModels
{
    public class EditUserViewModel
    {
        public int UserId { get; set; }

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
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        [EmailAddress(ErrorMessage = "The email entered is not valid")]
        public string? Email { get; set; }

        [Display(Name = "Phone Number")]
        [MaxLength(13, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Email is Active")]
        public bool IsEmailActive { get; set; }

        [Display(Name = "Phone Number is Active")]
        public bool IsPhoneNumberActive { get; set; }

        [Display(Name = "Ban User")]
        public bool IsBan { get; set; }

        [Display(Name = "New Password")]
        [StrongPassword]
        [MaxLength(200, ErrorMessage = "{0} cannot be more than {1} characters")]
        public string? NewPassword { get; set; }

        [Display(Name = "Confirm Password")]
        [Compare("NewPassword", ErrorMessage = "Password and confirmation do not match")]
        public string? ConfirmPassword { get; set; }

        // Role Management Properties
        [Display(Name = "User Roles")]
        public List<int> SelectedRoleIds { get; set; } = new List<int>();

        public List<RoleSelectionViewModel> AvailableRoles { get; set; } = new List<RoleSelectionViewModel>();
    }

    public class RoleSelectionViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public string DisplayName { get; set; }
        public string? Description { get; set; }
        public bool IsSelected { get; set; }
    }
}
