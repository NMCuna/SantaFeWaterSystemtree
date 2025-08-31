using System.ComponentModel.DataAnnotations;

public class TwoFactorSetupViewModel
{
    public int UserId { get; set; }
    public string Role { get; set; } = "Admin";

    public string QRCodeUrl { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Authentication Code")]
    public string Code { get; set; } = string.Empty;
}
