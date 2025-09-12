using System;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class PasswordHistory
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }   // FIX: int, not string

        [Required]
        public string? PasswordHash { get; set; }

        public DateTime ChangedDate { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User? User { get; set; }
    }
}
