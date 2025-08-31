using System.ComponentModel.DataAnnotations;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.ViewModels
{
    public class UserProfileViewModel
    {
        public int Id { get; set; }

        // Optional: Still used to compute FullName, but not posted
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;

        // ✅ Computed, read-only, not bound in form
        public string? FullName => $"{FirstName} {MiddleName} {LastName}".Replace("  ", " ").Trim();

        // ✅ Not editable — remove [Required] to avoid model validation error
        public string? Address { get; set; }

        [Phone(ErrorMessage = "Invalid phone number.")]
        public string? ContactNumber { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = string.Empty;

        // Read-only display
        public string? AccountNumber { get; set; }

        public ConsumerType? AccountType { get; set; }

        public string? MeterNo { get; set; }

        public IFormFile? ProfileImage { get; set; }  // for uploading
        public string? ExistingProfilePicture { get; set; }  // for displaying

    }
}
