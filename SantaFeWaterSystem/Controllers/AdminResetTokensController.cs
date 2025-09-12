using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminResetTokensController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminResetTokensController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Index - show all daily tokens
        public async Task<IActionResult> Index()
        {
            var tokens = await _context.AdminResetTokens.OrderBy(t => t.Id).ToListAsync();
            return View(tokens);
        }

        // GET: Edit token
        public async Task<IActionResult> Edit(int id)
        {
            var token = await _context.AdminResetTokens.FindAsync(id);
            if (token == null) return NotFound();
            return View(token);
        }

        // POST: Edit token
        // POST: Edit token
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminResetToken model)
        {
            if (!ModelState.IsValid)
            {
                // Log errors for debugging
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                Console.WriteLine("ModelState errors: " + errors);

                return View(model);
            }

            var tokenInDb = await _context.AdminResetTokens.FindAsync(model.Id);
            if (tokenInDb == null) return NotFound();

            // Update only the Token, keep Day intact
            tokenInDb.Token = model.Token;

            _context.AuditTrails.Add(new AuditTrail
            {
                PerformedBy = User.Identity?.Name ?? "Admin",
                Action = "Edit Admin Reset Token",
                Timestamp = DateTime.Now,
                Details = $"Token for {tokenInDb.Day} changed to '{tokenInDb.Token}'"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Token for {tokenInDb.Day} updated successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}
