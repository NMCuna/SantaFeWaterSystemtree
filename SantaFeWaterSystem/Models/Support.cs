using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class Support
    {
        public int Id { get; set; }

        [Required]
        public int ConsumerId { get; set; }

        [Required]
        [StringLength(100)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ResolvedAt { get; set; }

        public bool IsArchived { get; set; } = false;

        // ✅ Admin reply is nullable (correct)
        [StringLength(1000)]
        public string? AdminReply { get; set; }

        public DateTime? RepliedAt { get; set; }

        public bool IsResolved { get; set; } = false;
        public bool IsReplySeen { get; set; } = false;

        [ForeignKey("ConsumerId")]
        public Consumer Consumer { get; set; }


        // ✅ User feedback (emoji + note + date)
        public string? SupportFeedbackEmoji { get; set; }
        public string? SupportFeedbackNote { get; set; }
        public DateTime? SupportFeedbackAt { get; set; }
    }
}

