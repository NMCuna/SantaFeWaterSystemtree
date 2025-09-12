using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class LockoutPolicy
    {
        public int Id { get; set; }

        [Range(1, 20, ErrorMessage = "Max failed attempts must be between 1 and 20.")]
        public int MaxFailedAccessAttempts { get; set; } = 5;

        [Range(1, 120, ErrorMessage = "Lockout minutes must be between 1 and 120.")]
        public int LockoutMinutes { get; set; } = 15;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
