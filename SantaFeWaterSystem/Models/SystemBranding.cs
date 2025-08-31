using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class SystemBranding
    {
        public int Id { get; set; }

        [StringLength(100)]
        public string? SystemName { get; set; }  // nullable

        public string? IconClass { get; set; }   // nullable

        public string? LogoPath { get; set; }    // nullable
    }
}
