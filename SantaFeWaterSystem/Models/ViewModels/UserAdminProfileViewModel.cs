using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class UserAdminProfileViewModel
    {
        public int Id { get; set; }

        public string ExistingProfilePicture { get; set; } = "default.png";

        [Display(Name = "Upload Picture")]
        public IFormFile? ProfileImage { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string FullName { get; set; }

        public bool IsMfaEnabled { get; set; }

        public string Role { get; set; }

        public bool IsAdmin { get; set; }
    }
}
