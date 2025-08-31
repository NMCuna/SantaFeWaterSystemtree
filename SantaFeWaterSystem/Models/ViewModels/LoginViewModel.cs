using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

}
