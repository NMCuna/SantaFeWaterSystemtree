using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class Announcement
    {
        [Key]
        public int? Id { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(1000)]
        public string? Content { get; set; }

        public string? ImagePath { get; set; } // optional picture

        public int? AdminId { get; set; }

        [ForeignKey("AdminId")]
        public User? Admin { get; set; }

        public DateTime? PostedAt { get; set; } = DateTime.UtcNow;

        public bool? IsActive { get; set; } = true;

        public virtual ICollection<Feedback>? Feedbacks { get; set; }
    }
}
