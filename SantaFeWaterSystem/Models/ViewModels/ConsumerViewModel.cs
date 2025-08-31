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

        // Optional: Computed property to keep compatibility with old FullName
        public string FullName => $"{FirstName} {MiddleName} {LastName}".Replace("  ", " ").Trim();

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string? AccountNumber { get; set; }

        public string? ContactNumber { get; set; }

        public int? UserId { get; set; }

        public List<SelectListItem>? AvailableUsers { get; set; } // For dropdown

        public string? AccountType { get; set; }
        public string? MeterNo { get; set; }

    }
}
