using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class VisitorController(ApplicationDbContext context) : Controller
{
    // Primary constructor parameter becomes available in the whole class
    private readonly ApplicationDbContext _context = context;

    // Show live visitor counters
    public IActionResult Index() => View();

    // Show detailed logs
    public async Task<IActionResult> Details()
    {
        var logs = await _context.VisitorLogs
            .OrderByDescending(v => v.VisitedAtUtc)
            .ToListAsync();

        return View(logs);
    }
}
