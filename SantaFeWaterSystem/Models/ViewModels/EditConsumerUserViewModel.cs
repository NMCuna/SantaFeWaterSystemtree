using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class EditConsumerUserViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Required]
        [Display(Name = "Account Number")]
        public string? AccountNumber { get; set; }

        [Display(Name = "Enable Two-Factor Authentication")]
        public bool IsMfaEnabled { get; set; }

        public string? Role { get; set; }


      
        public string? RoleFilter { get; set; }
        public string? SearchTerm { get; set; }
        public int CurrentPage { get; set; }
    }
}
