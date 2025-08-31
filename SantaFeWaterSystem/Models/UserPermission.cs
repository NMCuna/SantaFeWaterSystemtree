using SantaFeWaterSystem.Models;
using System.ComponentModel.DataAnnotations;

public class UserPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [Required]
    public string PermissionKey { get; set; } = string.Empty;

    // Optional: Navigation property
    public User? User { get; set; }
}
