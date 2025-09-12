using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class AdminAccessSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string? LoginViewToken { get; set; }
    }
}
