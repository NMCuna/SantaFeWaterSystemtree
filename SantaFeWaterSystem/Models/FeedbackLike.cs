using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class FeedbackLike
    {
        [Key]
        public int? Id { get; set; }

        public int? FeedbackId { get; set; }

        [ForeignKey("FeedbackId")]
        public Feedback? Feedback { get; set; }

        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
        public string? Username { get; set; }

        public DateTime? LikedAt { get; set; } = DateTime.UtcNow;
    }
}
