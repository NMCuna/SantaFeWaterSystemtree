using System;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class PublicInquiry
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string? FirstName { get; set; }

        [Required, StringLength(50)]
        public string? LastName { get; set; }

        [Required, EmailAddress]
        public string? Email { get; set; }

        [Required, Phone]
        public string? PhoneNumber { get; set; }

        [Required, StringLength(100)]
        public string? Purpose { get; set; }

        [Required, StringLength(1000)]
        public string? Description { get; set; }

        // No validation attribute, handled only in UI
        public bool IsAgreed { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? AdminResponse { get; set; }        // set after reply
        public string Status { get; set; } = "New";        // New, Replied
        public DateTime? RepliedAt { get; set; }           // set after reply
    }
}
