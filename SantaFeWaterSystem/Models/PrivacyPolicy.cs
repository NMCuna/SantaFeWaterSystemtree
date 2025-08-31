using System;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class PrivacyPolicy
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Privacy Policy content is required.")]
        public string Content { get; set; } = string.Empty;

        // Increment this every time the admin publishes an update
        [Required]
        public int Version { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for sections
        public List<PrivacyPolicySection> Sections { get; set; } = new List<PrivacyPolicySection>();


    }
}
