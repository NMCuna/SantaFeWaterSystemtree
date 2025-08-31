using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models.ViewModels
{
    public class ForgotPasswordUserViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }
    }
}
