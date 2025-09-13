using System.ComponentModel.DataAnnotations;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.ViewModels
{
    public class EditConsumerViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Last Name is required")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "First Name is required")]
        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Account Type is required")]
        public ConsumerType AccountType { get; set; }

        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Home Address is required")]
        public string HomeAddress { get; set; } = string.Empty;

        public string? MeterAddress { get; set; }
        public string? MeterNo { get; set; }
        public string? ContactNumber { get; set; }

        [Required(ErrorMessage = "Status is required")]
        public string Status { get; set; } = "Active";

        public int? UserId { get; set; } // Linked User
    }
}
