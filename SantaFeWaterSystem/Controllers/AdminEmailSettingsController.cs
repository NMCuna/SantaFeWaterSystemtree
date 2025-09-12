using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.Controllers
{
    public class AdminEmailSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminEmailSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _context.EmailSettings.FirstOrDefaultAsync();
            if (settings == null)
                return RedirectToAction("Create");

            return View(settings);
        }

        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(EmailSettings settings)
        {
            if (!ModelState.IsValid) return View(settings);

            _context.Add(settings);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var settings = await _context.EmailSettings.FindAsync(id);
            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(EmailSettings settings)
        {
            if (!ModelState.IsValid) return View(settings);

            _context.Update(settings);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
