using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;

[Authorize(Roles = "Admin")]
public class LockoutPolicyController : Controller
{
    private readonly ApplicationDbContext _context;

    public LockoutPolicyController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var policy = await _context.LockoutPolicies.FirstOrDefaultAsync();
        return View(policy ?? new LockoutPolicy());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LockoutPolicy model)
    {
        if (!ModelState.IsValid) return View(model);

        var existing = await _context.LockoutPolicies.FirstOrDefaultAsync();
        if (existing == null)
        {
            _context.LockoutPolicies.Add(model);
        }
        else
        {
            existing.MaxFailedAccessAttempts = model.MaxFailedAccessAttempts;
            existing.LockoutMinutes = model.LockoutMinutes;
            existing.LastUpdated = DateTime.UtcNow;
            _context.LockoutPolicies.Update(existing);
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Lockout policy updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
