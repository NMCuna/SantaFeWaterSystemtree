using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class AdminResetPasswordViewModel
    {
        [Required(ErrorMessage = "Please select an admin.")]
        public int? AdminId { get; set; }                  // Nullable for validation

        [Required(ErrorMessage = "New password is required.")]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }           // Nullable reference type

        [Required(ErrorMessage = "Confirm password is required.")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string? ConfirmPassword { get; set; }       // Nullable reference type

        [Required]
        public string? Token { get; set; }                 // Nullable reference type

        public List<AdminDropdown>? AdminList { get; set; } // Optional dropdown list

        public SantaFeWaterSystem.Models.PasswordPolicy? PasswordPolicy { get; set; } // Optional policy info

        public string? CurrentTime { get; set; }           // Optional display
        public string? TimeWarning { get; set; }           // Optional display
    }

    public class AdminDropdown
    {
        public int Id { get; set; }
        public string? Username { get; set; }              // Nullable reference type
    }
}
