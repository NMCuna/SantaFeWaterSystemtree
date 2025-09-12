using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    // Optional: restrict to admins
    // [Authorize(Roles = "Admin")]
    public class AdminSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Show the single token (Index)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var setting = await _context.AdminAccessSettings.FirstOrDefaultAsync();

            // Safety fallback: if no record exists (e.g. DB created manually), create one
            if (setting == null)
            {
                setting = new AdminAccessSetting { LoginViewToken = "wako-kabalo-ganiiiii" };
                _context.AdminAccessSettings.Add(setting);
                await _context.SaveChangesAsync();
            }

            return View(setting);
        }

        // Edit (GET)
        [HttpGet]
        public async Task<IActionResult> EditToken()
        {
            var setting = await _context.AdminAccessSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                // create fallback if missing
                setting = new AdminAccessSetting { LoginViewToken = "wako-kabalo-ganiiiii" };
                _context.AdminAccessSettings.Add(setting);
                await _context.SaveChangesAsync();
            }
            return View(setting);
        }

        // Edit (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditToken(AdminAccessSetting model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var setting = await _context.AdminAccessSettings.FirstOrDefaultAsync();
            if (setting != null)
            {
                setting.LoginViewToken = model.LoginViewToken;
                _context.Update(setting);
            }
            else
            {
                _context.AdminAccessSettings.Add(new AdminAccessSetting { LoginViewToken = model.LoginViewToken });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Admin Login Token updated!";
            return RedirectToAction("Index");
        }
    }
}
