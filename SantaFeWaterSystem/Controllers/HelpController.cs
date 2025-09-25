using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    public class HelpController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HelpController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================= INDEX =================
        public async Task<IActionResult> Index()
        {
            var helps = await _context.Helps
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
            return View(helps); // Views/Help/Index.cshtml
        }


        // ================= PUBLIC / HOME =================
        [AllowAnonymous]
        public async Task<IActionResult> HomeHelp()
        {
            var helps = await _context.Helps
                .Where(h => h.RoleAccess == "Home" || h.RoleAccess == "All" || h.RoleAccess == null)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();

            return View(helps); // Views/Help/HomeHelp.cshtml
        }


        // ================= CONSUMER HELP =================
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Consumer()
        {
            var helps = await _context.Helps
                .Where(h => h.RoleAccess == "User" || h.RoleAccess == "All" || h.RoleAccess == null)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
            return View(helps); // Views/Help/Consumer.cshtml
        }

        // ================= STAFF HELP =================
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> Staff()
        {
            var helps = await _context.Helps
                .Where(h => h.RoleAccess == "Staff" || h.RoleAccess == "All" || h.RoleAccess == null)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
            return View(helps); // Views/Help/Staff.cshtml
        }

        // ================= ADMIN HELP =================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin()
        {
            var helps = await _context.Helps
                .Where(h => h.RoleAccess == "Admin" || h.RoleAccess == "All" || h.RoleAccess == null)
                .OrderByDescending(h => h.CreatedAt)
                .ToListAsync();
            return View(helps); // Views/Help/Admin.cshtml
        }

        // ================= DETAILS =================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var help = await _context.Helps.FirstOrDefaultAsync(h => h.Id == id);
            if (help == null)
                return NotFound();

            // No need for extra RoleAccess check here
            return View(help);
        }


        // ================= CREATE MULTIPLE =================

        [Authorize(Roles = "Admin")]
        public IActionResult CreateMultiple()
        {
            // Initialize 3 empty Help items by default
            var model = new List<Help> { new Help(), new Help(), new Help() };
            return View(model); // Views/Help/CreateMultiple.cshtml
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMultiple(List<Help> helps)
        {
            if (ModelState.IsValid)
            {
                foreach (var help in helps)
                {
                    if (!string.IsNullOrWhiteSpace(help.Title) && !string.IsNullOrWhiteSpace(help.Description))
                    {
                        help.CreatedAt = DateTime.Now;
                        _context.Helps.Add(help);
                    }
                }
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(helps);
        }

        // ================= EDIT =================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var help = await _context.Helps.FindAsync(id);
            if (help == null) return NotFound();
            return View(help); // Views/Help/Edit.cshtml
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Help help)
        {
            if (id != help.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(help);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Helps.AnyAsync(h => h.Id == help.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(help);
        }

        // ================= DELETE =================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var help = await _context.Helps.FindAsync(id);
            if (help == null) return NotFound();
            return View(help); // Views/Help/Delete.cshtml
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var help = await _context.Helps.FindAsync(id);
            if (help != null)
            {
                _context.Helps.Remove(help);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
