using System;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public int? ConsumerId { get; set; } // Nullable means it can be null for broadcast

        public Consumer? Consumer { get; set; }

        [Required]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;


        public bool IsRead { get; set; } = false;
        public bool IsArchived { get; set; } = false;

        // ✅ Indicates if this is a broadcast to all consumers
        public bool SendToAll { get; set; } = false;
    }
}
