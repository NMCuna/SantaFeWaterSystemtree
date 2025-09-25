using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models.ViewModels
{
    public class ConsumerViewModel
    {
        public int? Id { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty;

        public string? MiddleName { get; set; }

        [Required]
        public string LastName { get; set; } = string.Empty;

        // Computed property (useful if you want to show full name in lists)
        public string FullName => $"{FirstName} {MiddleName} {LastName}".Replace("  ", " ").Trim();

        [Required]
        [Display(Name = "Home Address")]
        public string HomeAddress { get; set; } = string.Empty;

        [Display(Name = "Meter Address")]
        public string? MeterAddress { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Contact Number")]
        public string? ContactNumber { get; set; }

        [Display(Name = "Account Type")]
        [Required]
        public ConsumerType AccountType { get; set; }

        [Display(Name = "Meter No.")]
        public string? MeterNo { get; set; }

        [Display(Name = "Linked User")]
        public int? UserId { get; set; }

        // Dropdowns
        public IEnumerable<SelectListItem>? AvailableUsers { get; set; }
        public IEnumerable<SelectListItem>? AccountTypes { get; set; }
    }
}
