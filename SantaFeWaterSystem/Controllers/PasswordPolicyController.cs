using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    public class PasswordPolicyController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PasswordPolicyController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Show Password Policy
        public async Task<IActionResult> Index()
        {
            var policy = await _context.PasswordPolicies.FirstOrDefaultAsync();

            // If no policy exists, create a blank one
            if (policy == null)
            {
                policy = new PasswordPolicy();
                _context.PasswordPolicies.Add(policy);
                await _context.SaveChangesAsync();
            }

            return View(policy);
        }

        // POST: Save/Update Password Policy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(PasswordPolicy policy)
        {
            if (!ModelState.IsValid)
            {
                return View(policy);
            }

            var existingPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();

            if (existingPolicy == null)
            {
                // No record yet, add new
                _context.PasswordPolicies.Add(policy);
            }
            else
            {
                // Update fields explicitly
                existingPolicy.MinPasswordLength = policy.MinPasswordLength;
                existingPolicy.RequireComplexity = policy.RequireComplexity;
                existingPolicy.PasswordHistoryCount = policy.PasswordHistoryCount;
                existingPolicy.MaxPasswordAgeDays = policy.MaxPasswordAgeDays;
                existingPolicy.MinPasswordAgeDays = policy.MinPasswordAgeDays;
                // Add other fields here if needed
            }

            await _context.SaveChangesAsync();
            ViewBag.Message = "Password Policy Updated Successfully!";

            // Reload saved data
            var savedPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();
            return View(savedPolicy);
        }
    }
}
