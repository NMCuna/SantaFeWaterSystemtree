using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class Feedback
    {
        [Key]
        public int? Id { get; set; }

        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Range(1, 5)]
        public int? Rating { get; set; }

        [MaxLength(500)]
        public string? Comment { get; set; }

        public string? ImagePath { get; set; } // optional image

        public string? Reply { get; set; } = null;

        public string? Status { get; set; } = "Unread";

        public bool? IsArchived { get; set; } = false;

        public DateTime? SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RepliedAt { get; set; }

        public int? AnnouncementId { get; set; }

        [ForeignKey("AnnouncementId")]
        public Announcement? Announcement { get; set; }
        public string? Username { get; set; } 

        // ✅ Add navigation properties for comments and likes
        public virtual ICollection<FeedbackComment>? Comments { get; set; }

        public virtual ICollection<FeedbackLike>? FeedbackLikes { get; set; }
    }
}
