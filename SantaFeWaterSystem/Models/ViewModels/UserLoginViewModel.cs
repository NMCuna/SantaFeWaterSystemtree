// ViewModels/UserLoginViewModel.cs
using System.ComponentModel.DataAnnotations;

public class UserLoginViewModel
{
    [Required]
    public string AccountNumber { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}
