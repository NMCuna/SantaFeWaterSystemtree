using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class UserRegisterViewModel
    {
        [Required]
        public string AccountNumber { get; set; }

        [Required]
        public string Username { get; set; } // ✅ Added Username

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
