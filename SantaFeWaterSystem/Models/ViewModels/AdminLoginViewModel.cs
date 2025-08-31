// ViewModels/AdminLoginViewModel.cs
using System.ComponentModel.DataAnnotations;

public class AdminLoginViewModel
{
    [Required]
    public string Username { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}
