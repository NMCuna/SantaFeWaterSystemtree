using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Data;
using System.Linq;

namespace SantaFeWaterSystem.Controllers
{
    // Primary constructor injects ApplicationDbContext
    public class ConsumerSettingController(ApplicationDbContext _context) : Controller
    {
        public IActionResult Index()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;

            bool is2FAEnabled = false;
            if (int.TryParse(userIdClaim, out int userId))
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == userId);
                if (user != null)
                {
                    is2FAEnabled = user.IsMfaEnabled;
                }
            }

            ViewBag.Is2FAEnabled = is2FAEnabled;

            return View();
        }
    }
}
