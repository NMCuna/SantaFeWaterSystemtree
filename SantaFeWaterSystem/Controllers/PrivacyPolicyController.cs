using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Models.ViewModels;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class PrivacyPolicyController(ApplicationDbContext context, AuditLogService audit) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        protected readonly AuditLogService _audit = audit;



        //================== AGREE POLICY VIEW LIST ==================

        // Show the latest policy
        public async Task<IActionResult> Index()
        {
            var policy = await _context.PrivacyPolicies
                .Include(p => p.Sections)
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync();

            return View(policy);
        }



        //================== AGREE POLICY CREATE ==================

        // GET: Show Create Form with one default empty section
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var latestVersion = await _context.PrivacyPolicies
                .OrderByDescending(p => p.Version)
                .Select(p => p.Version)
                .FirstOrDefaultAsync();

            var model = new CreatePrivacyPolicyViewModel
            {
                Version = latestVersion + 1,
                Sections =
        {
           new()
            {
                SectionTitle = "",
                Content = "",
                IsActive = true
            }
        }
            };

            return View(model);
        }

        // POST: Save new policy version
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePrivacyPolicyViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var latestVersion = await _context.PrivacyPolicies
                .OrderByDescending(p => p.Version)
                .Select(p => p.Version)
                .FirstOrDefaultAsync();

            // Handle all sections properly
            var sections = model.Sections
                .Select((s, i) => new PrivacyPolicySection
                {
                    SectionTitle = s.SectionTitle,
                    Content = s.Content,
                    IsActive = Request.Form[$"Sections[{i}].IsActive"] == "true" // checked = true, else false
                })
                .ToList();

            var newPolicy = new PrivacyPolicy
            {
                Title = model.Title,
                Content = model.Content,
                Version = latestVersion + 1,
                CreatedAt = DateTime.UtcNow,
                Sections = sections
            };

            _context.PrivacyPolicies.Add(newPolicy);
            await _context.SaveChangesAsync();

            // Audit trail
            var performedBy = User.Identity?.Name ?? "Unknown";
            var sectionTitles = string.Join(", ", sections.Select(s => s.SectionTitle));
            var details = $"Created Privacy Policy v{newPolicy.Version} - Title: {newPolicy.Title}, Sections: {sectionTitles}";
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
            var audit = new AuditTrail
            {
                Action = "Create Privacy Policy",
                PerformedBy = performedBy,
                Timestamp = DateTime.UtcNow,
                Details = details
            };

            _context.AuditTrails.Add(audit);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Privacy Policy v{newPolicy.Version} created successfully.";
            return RedirectToAction(nameof(Index));
        }



        //================== AGREEMENT LIST ==================
        // Paginated list of all user agreements
        public async Task<IActionResult> PrivacyAgreements(int page = 1, int pageSize = 10)
        {
            var totalAgreements = await _context.UserPrivacyAgreements.CountAsync();

            var totalPages = (int)Math.Ceiling(totalAgreements / (double)pageSize);
            totalPages = Math.Max(totalPages, 1);

            page = Math.Max(1, page);
            page = Math.Min(page, totalPages);

            var agreements = await _context.UserPrivacyAgreements
                .Include(a => a.Consumer)
                .ThenInclude(c => c.User)
                .OrderByDescending(a => a.AgreedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(agreements);
        }

    }
}
