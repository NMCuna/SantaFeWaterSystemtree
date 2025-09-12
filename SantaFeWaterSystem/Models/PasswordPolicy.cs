using System;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class PasswordPolicy
    {
        public int Id { get; set; }

        [Range(0, 365, ErrorMessage = "Maximum password age must be between 0 and 365 days.")]
        public int MaxPasswordAgeDays { get; set; } = 90;

        [Range(0, 365, ErrorMessage = "Minimum password age must be between 0 and 365 days.")]
        public int MinPasswordAgeDays { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Minimum password length must be at least 1.")]
        public int MinPasswordLength { get; set; } = 8;

        [Range(0, 50, ErrorMessage = "Password history count must be between 0 and 50.")]
        public int PasswordHistoryCount { get; set; } = 5;

        public bool RequireComplexity { get; set; } = true;
    }
}
