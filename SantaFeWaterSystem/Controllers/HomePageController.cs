// Controllers/Admin/HomePageController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers.Admin
{
    [Authorize(Roles = "Admin,Staff")]
    public class HomePageController : Controller
    {
        private readonly ApplicationDbContext _context;
        protected readonly AuditLogService _audit;

        public HomePageController(ApplicationDbContext context, AuditLogService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index()
        {
            var content = await _context.HomePageContents.FirstOrDefaultAsync();
            return View(content);
        }

        public IActionResult Create()
        {
            return View();
        }

        // POST: HomePageContent/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HomePageContent model)
        {
            if (ModelState.IsValid)
            {
                _context.HomePageContents.Add(model);
                await _context.SaveChangesAsync();

                // ✅ Audit trail
                var performedBy = User.Identity?.Name ?? "Unknown";
                var details = $"New homepage content created. Title={model.Title}, Description={model.Subtitle}";
                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
                var audit = new AuditTrail
                {
                    Action = "Create HomePageContent",
                    PerformedBy = performedBy,
                    Timestamp = DateTime.Now,
                    Details = details
                };
                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }


        public async Task<IActionResult> Edit(int id)
        {
            var content = await _context.HomePageContents.FindAsync(id);
            if (content == null) return NotFound();
            return View(content);
        }

        // POST: HomePageContent/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(HomePageContent model)
        {
            if (ModelState.IsValid)
            {
                // Get original content for audit
                var originalContent = await _context.HomePageContents.AsNoTracking()
                                            .FirstOrDefaultAsync(c => c.Id == model.Id);

                _context.Update(model);
                await _context.SaveChangesAsync();

                // ✅ Audit trail
                var performedBy = User.Identity?.Name ?? "Unknown";
                var details = $"Homepage content updated. " +
                              $"Before: Title={originalContent.Title}, Description={originalContent.Subtitle}. " +
                              $"After: Title={model.Title}, Description={model.Subtitle}";
                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
                var audit = new AuditTrail
                {
                    Action = "Edit HomePageContent",
                    PerformedBy = performedBy,
                    Timestamp = DateTime.Now,
                    Details = details
                };
                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }


        // GET: show confirmation
        public async Task<IActionResult> Delete(int id)
        {
            var content = await _context.HomePageContents.FindAsync(id);
            if (content == null) return NotFound();
            return View(content);
        }

        // POST: HomePageContent/DeleteConfirmed
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var content = await _context.HomePageContents.FindAsync(id);
            if (content == null) return NotFound();

            _context.HomePageContents.Remove(content);
            await _context.SaveChangesAsync();

            // ✅ Audit trail
            var performedBy = User.Identity?.Name ?? "Unknown";
            var details = $"Homepage content deleted. Title={content.Title}, Description={content.Subtitle}";
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
            var audit = new AuditTrail
            {
                Action = "Delete HomePageContent",
                PerformedBy = performedBy,
                Timestamp = DateTime.Now,
                Details = details
            };
            _context.AuditTrails.Add(audit);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

    }
}
