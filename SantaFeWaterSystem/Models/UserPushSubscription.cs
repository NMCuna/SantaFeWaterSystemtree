using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class UserPushSubscription
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; } // your custom user ID
        public User User { get; set; }

        public string Endpoint { get; set; }
        public string P256DH { get; set; }
        public string Auth { get; set; }
    }
}
