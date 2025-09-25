using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class EditAdminUserViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required.")]
        [StringLength(50, ErrorMessage = "Username must be at most 50 characters.")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name must be at most 100 characters.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Enable MFA")]
        public bool IsMfaEnabled { get; set; }

        public string Role { get; set; } = string.Empty; // Admin or Staff

        // For keeping filters/paging
        public string? RoleFilter { get; set; }
        public string? SearchTerm { get; set; }
        public int CurrentPage { get; set; } = 1;
    }
}
