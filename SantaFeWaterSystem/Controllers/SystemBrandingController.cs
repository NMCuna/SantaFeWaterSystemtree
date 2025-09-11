using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class SystemBrandingController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, AuditLogService audit)
    : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;
        protected readonly AuditLogService _audit = audit;




        ///////////////////////////////////////////////////////////////
        //      MANAGE LANDING PAGE (HOME) /ADMIN/STAFF ACTION       //
        ///////////////////////////////////////////////////////////////




        // ================== INDEX (SHOW THE LIST OF NEW CREATED) ==================

        // READ - LIST ALL
        public async Task<IActionResult> Index()
        {
            var brandings = await _context.SystemBrandings.ToListAsync();
            return View(brandings);
        }




        // ================== CREATE (ADD NEW) ==================

        // CREATE (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }


        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SystemBranding model, IFormFile LogoFile)
        {
            if (ModelState.IsValid)
            {
                // Handle logo upload
                if (LogoFile != null && LogoFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

                    // Create folder if not exists
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid() + Path.GetExtension(LogoFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await LogoFile.CopyToAsync(fileStream);
                    }

                    model.LogoPath = "/images/" + uniqueFileName;
                }

                _context.SystemBrandings.Add(model);
                await _context.SaveChangesAsync();

                // Audit trail with PH time
                var performedBy = User.Identity?.Name ?? "Unknown";
                var details = $"Created new system branding. SystemName: {model.SystemName}, LogoPath: {model.LogoPath}";

                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

                var audit = new AuditTrail
                {
                    Action = "Create System Branding",
                    PerformedBy = performedBy,
                    Timestamp = timestampPH,
                    Details = details
                };

                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();


                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }





        // ================== EDIT (UPDATE EXISTING) ==================

        // EDIT (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var branding = await _context.SystemBrandings.FindAsync(id);
            if (branding == null) return NotFound();
            return View(branding);
        }

        // EDIT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SystemBranding model, IFormFile LogoFile)
        {
            if (ModelState.IsValid)
            {
                var branding = await _context.SystemBrandings.AsNoTracking()
                                       .FirstOrDefaultAsync(b => b.Id == model.Id);
                if (branding == null) return NotFound();

                // Keep original values for audit
                var originalSystemName = branding.SystemName;
                var originalIconClass = branding.IconClass;
                var originalLogoPath = branding.LogoPath;

                // Update values
                branding.SystemName = model.SystemName;
                branding.IconClass = model.IconClass;

                if (LogoFile != null && LogoFile.Length > 0)
                {
                    // Delete old logo if exists
                    if (!string.IsNullOrEmpty(branding.LogoPath))
                    {
                        var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, branding.LogoPath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    var uploads = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                    if (!Directory.Exists(uploads))
                        Directory.CreateDirectory(uploads);

                    var uniqueFileName = Guid.NewGuid() + Path.GetExtension(LogoFile.FileName);
                    var filePath = Path.Combine(uploads, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await LogoFile.CopyToAsync(fileStream);
                    }

                    branding.LogoPath = "/images/" + uniqueFileName;
                }

                _context.SystemBrandings.Update(branding);
                await _context.SaveChangesAsync();

                // Audit trail with PH time
                var performedBy = User.Identity?.Name ?? "Unknown";
                var details = $"Edited System Branding. " +
                              $"SystemName: {originalSystemName} -> {branding.SystemName}, " +
                              $"IconClass: {originalIconClass} -> {branding.IconClass}, " +
                              $"LogoPath: {originalLogoPath} -> {branding.LogoPath}";

                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

                var audit = new AuditTrail
                {
                    Action = "Edit System Branding",
                    PerformedBy = performedBy,
                    Timestamp = timestampPH,
                    Details = details
                };

                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }




        // ================== DELETE (REMOVE EXISTING) ==================

        // DELETE (GET confirmation page)
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var branding = await _context.SystemBrandings.FindAsync(id);
            if (branding == null) return NotFound();
            return View(branding);
        }

        // DELETE (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var branding = await _context.SystemBrandings.FindAsync(id);
            if (branding != null)
            {
                // Keep values for audit
                var systemName = branding.SystemName;
                var iconClass = branding.IconClass;
                var logoPath = branding.LogoPath;

                // Delete logo file
                if (!string.IsNullOrEmpty(branding.LogoPath))
                {
                    var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, branding.LogoPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                _context.SystemBrandings.Remove(branding);
                await _context.SaveChangesAsync();

                // Audit trail
                var performedBy = User.Identity?.Name ?? "Unknown";
                var details = $"Deleted System Branding. SystemName: {systemName}, IconClass: {iconClass}, LogoPath: {logoPath}";

                var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
                var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

                var audit = new AuditTrail
                {
                    Action = "Delete System Branding",
                    PerformedBy = performedBy,
                    Timestamp = DateTime.UtcNow,
                    Details = details
                };

                _context.AuditTrails.Add(audit);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
