using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class UserRegisterViewModel
    {
        [Required(ErrorMessage = "Account number is required.")]
        public string? AccountNumber { get; set; }

        [Required(ErrorMessage = "Username is required.")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }
}
