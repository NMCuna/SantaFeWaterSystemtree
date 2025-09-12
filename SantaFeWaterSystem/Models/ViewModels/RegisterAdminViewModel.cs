using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class RegisterAdminViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Username is required.")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        public string? Role { get; set; }
    }
}
