using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

public class AuditTrailViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public AuditTrailViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // Get the 5 latest audit logs
        var recentLogs = await _context.AuditTrails
                            .OrderByDescending(a => a.Timestamp)
                            .Take(5)
                            .ToListAsync();

        // Pass them to the view
        return View(recentLogs);
    }
}
