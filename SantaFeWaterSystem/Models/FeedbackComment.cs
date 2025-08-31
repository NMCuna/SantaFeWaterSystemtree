using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class FeedbackComment
    {
        [Key]
        public int? Id { get; set; }

        public int? FeedbackId { get; set; }

        [ForeignKey("FeedbackId")]
        public Feedback? Feedback { get; set; }

        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required, MaxLength(300)]
        public string? Content { get; set; }

        public DateTime? CommentedAt { get; set; } = DateTime.UtcNow;
    }
}
