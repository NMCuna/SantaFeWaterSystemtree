using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class EmailLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmailLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: EmailLogs
        public async Task<IActionResult> Index()
        {
            var emailLogs = await _context.EmailLogs
                .Include(e => e.Consumer)
                .OrderByDescending(e => e.SentAt)
                .ToListAsync();
            return View(emailLogs);
        }

        // GET: EmailLogs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var emailLog = await _context.EmailLogs
                .Include(e => e.Consumer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (emailLog == null)
                return NotFound();

            return View(emailLog);
        }
    }
}
