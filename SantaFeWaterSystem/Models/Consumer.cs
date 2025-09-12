using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class Consumer
    {
        public int Id { get; set; }

        [Required]
        public ConsumerType AccountType { get; set; }

        // ✅ REPLACEMENT for FullName
        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";


        public string? MiddleName { get; set; }

        // ✅ REPLACEMENT for Address
        [Required]
        public string HomeAddress { get; set; } = string.Empty;

        public string? MeterAddress { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; } = string.Empty;

        public string? ContactNumber { get; set; }

        public int? UserId { get; set; }  // Nullable FK
        public User? User { get; set; }

        public string? MeterNo { get; set; }
        // ✅ Add this property to fix the error
        public string Status { get; set; } = "Active"; 
        public string? ProfilePicture { get; set; }

        public bool IsDisconnected { get; set; } = false;


        public ICollection<Notification>? Notifications { get; set; }
        public ICollection<Billing> Billings { get; set; } = new List<Billing>();
        public ICollection<EmailLog> EmailLogs { get; set; }

    }
}
