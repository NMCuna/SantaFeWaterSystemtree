using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using SantaFeWaterSystem.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace SantaFeWaterSystem.ViewComponents
{
    public class UserAvatarViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public UserAvatarViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public IViewComponentResult Invoke()
        {
            var userIdClaim = HttpContext.User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return View("Default", "~/images/default-avatar.png");
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            var profileImagePath = string.IsNullOrEmpty(user?.ProfileImageUrl)
                ? "~/images/default-avatar.png"
                : $"~/images/profiles/{user.ProfileImageUrl}";

            return View("Default", profileImagePath);
        }
    }
}
