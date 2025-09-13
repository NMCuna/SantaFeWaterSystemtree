using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Helpers;
using SantaFeWaterSystem.Models;
using X.PagedList;

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
        public async Task<IActionResult> Index(
            string? searchTerm,
            int? selectedMonth,
            int? selectedYear,
            int page = 1,
            int pageSize = 10
        )
        {
            var query = _context.EmailLogs
                .Include(e => e.Consumer)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e =>
                    e.Subject!.Contains(searchTerm) ||
                    e.Message!.Contains(searchTerm) ||
                    e.Consumer!.FullName!.Contains(searchTerm)
                );
            }

            // Month/Year filters
            if (selectedMonth.HasValue)
                query = query.Where(e => e.SentAt.Month == selectedMonth.Value);

            if (selectedYear.HasValue)
                query = query.Where(e => e.SentAt.Year == selectedYear.Value);

            // Order by latest first
            query = query.OrderByDescending(e => e.SentAt);

            // ✅ Async pagination
            var pagedEmailLogs = await query.ToPagedListAsync(page, pageSize);

            // Pass filters to View
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            return View(pagedEmailLogs);
        }


        // GET: EmailLogs/Details/5
        public async Task<IActionResult> Details(
            int? id,
            string? searchTerm,
            int? selectedMonth,
            int? selectedYear,
            int page = 1
        )
        {
            if (id == null) return NotFound();

            var emailLog = await _context.EmailLogs
                .Include(e => e.Consumer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (emailLog == null) return NotFound();

            // Pass filters & page to view
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.CurrentPage = page;

            return View(emailLog);
        }
    }
}
