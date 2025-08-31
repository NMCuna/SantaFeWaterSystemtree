using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class PrivacyPolicySection
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Section title is required.")]
        public string SectionTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Section content is required.")]
        public string Content { get; set; } = string.Empty;

        // Checkbox to include or exclude this section
        public bool IsActive { get; set; } = true;

        // Foreign key to PrivacyPolicy
        [Required]
        public int PrivacyPolicyId { get; set; }

        [ForeignKey("PrivacyPolicyId")]
        public PrivacyPolicy PrivacyPolicy { get; set; } = null!;
    }
}
