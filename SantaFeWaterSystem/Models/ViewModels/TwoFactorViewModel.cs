using System.ComponentModel.DataAnnotations;

public class TwoFactorViewModel
{
    public int UserId { get; set; }

    public string Role { get; set; } = "Admin"; // or "User"

    [Required]
    [Display(Name = "Authentication Code")]
    public string Code { get; set; } = string.Empty;
}
