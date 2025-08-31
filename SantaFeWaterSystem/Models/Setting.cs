using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class Setting
    {
        public int Id { get; set; }

        [StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Value { get; set; }
    }
}
