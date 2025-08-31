using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Data;
using System.Security.Claims;
using System.Threading.Tasks;

public class TwoFactorStatusAdminViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public TwoFactorStatusAdminViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // 🔍 Use the ViewComponent's UserClaimsPrincipal (safer than HttpContext directly)
        var userIdClaim = UserClaimsPrincipal?.FindFirst("UserId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
        {
            return View("Default", false); // fallback if not authenticated
        }

        var user = await _context.Users.FindAsync(userId);
        bool isEnabled = user?.IsMfaEnabled ?? false;

        return View("Default", isEnabled);
    }
}
