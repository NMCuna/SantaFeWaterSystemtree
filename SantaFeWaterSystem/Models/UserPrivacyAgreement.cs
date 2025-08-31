using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class UserPrivacyAgreement
    {
        public int Id { get; set; }

        // Link to your Consumer entity (required)
        [Required]
        public int ConsumerId { get; set; }

        [ForeignKey(nameof(ConsumerId))]
        public Consumer Consumer { get; set; } = default!;

        // The version number the consumer agreed to
        [Required]
        public int PolicyVersion { get; set; }

        public DateTime AgreedAt { get; set; } = DateTime.UtcNow;

        // (Optional but handy) store the specific policy record they agreed to
        public int? PrivacyPolicyId { get; set; }

        [ForeignKey(nameof(PrivacyPolicyId))]
        public PrivacyPolicy? Policy { get; set; }
    }
}
