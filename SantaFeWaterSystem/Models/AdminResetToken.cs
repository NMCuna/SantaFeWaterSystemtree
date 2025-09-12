using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class AdminResetToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string? Day { get; set; }  // e.g. Monday, Tuesday

        [Required]
        [MaxLength(200)]
        public string? Token { get; set; }
    }
}
