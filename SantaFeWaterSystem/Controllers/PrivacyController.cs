using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "User")]
    public class PrivacyController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;





        //================== AGREE POLICY VIEW ==================

        // GET: Privacy/Agree
        [HttpGet]
        public async Task<IActionResult> Agree()
        {
            var latestPolicy = await _context.PrivacyPolicies
                .Include(p => p.Sections) // Include related sections if you have them
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync();

            if (latestPolicy == null)
                return NotFound("Privacy policy not found.");

            return View(latestPolicy);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgreePolicy(int policyVersion)
        {
            // Get current logged-in username (account number or username)
            var usernameOrAccountNumber = User.Identity?.Name;

            // Get the User record
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == usernameOrAccountNumber
                                       || u.AccountNumber == usernameOrAccountNumber);

            if (user == null)
                return Unauthorized("User not found.");

            // Find the linked Consumer
            var consumer = await _context.Consumers
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (consumer == null)
                return Unauthorized("Linked consumer not found.");

            // Check if already agreed to this version
            var existingAgreement = await _context.UserPrivacyAgreements
                .FirstOrDefaultAsync(a => a.ConsumerId == consumer.Id && a.PolicyVersion == policyVersion);

            if (existingAgreement == null)
            {
                var agreement = new UserPrivacyAgreement
                {
                    ConsumerId = consumer.Id,
                    PolicyVersion = policyVersion,
                    AgreedAt = DateTime.UtcNow
                };

                _context.UserPrivacyAgreements.Add(agreement);
                await _context.SaveChangesAsync();
            }

            // ================== AUDIT TRAIL ==================
            var performedBy = User.Identity?.Name ?? "Unknown";

            // Optionally, get policy title and sections for details
            var policy = await _context.PrivacyPolicies
                .Include(p => p.Sections)
                .FirstOrDefaultAsync(p => p.Version == policyVersion);

            var sectionTitles = policy != null
                ? string.Join(", ", policy.Sections.Select(s => s.SectionTitle))
                : string.Empty;

            var details = $"User agreed to Privacy Policy v{policyVersion}" +
                          (policy != null ? $" - Title: {policy.Title}, Sections: {sectionTitles}" : "");

            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

            var audit = new AuditTrail
            {
                Action = "Agree Privacy Policy",
                PerformedBy = performedBy,
                Timestamp = timestampPH,
                Details = details
            };

            _context.AuditTrails.Add(audit);
            await _context.SaveChangesAsync();
        

            // Redirect to Consumer Dashboard
            return RedirectToAction("Dashboard", "User");
        }
    }
}
