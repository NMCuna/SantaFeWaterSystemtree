using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "Current Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [Required(ErrorMessage = "New Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmPassword { get; set; }
    }
}
