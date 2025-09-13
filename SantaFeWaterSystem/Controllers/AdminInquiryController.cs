using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Threading.Tasks;
using X.PagedList;
using X.PagedList.Extensions;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class AdminInquiryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public AdminInquiryController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        // GET: PublicInquiries
        public IActionResult Index(
            string searchTerm,
            int? selectedMonth,
            int? selectedYear,
            int page = 1,
            int pageSize = 10
        )
        {
            // Start with IQueryable
            var query = _context.PublicInquiries.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(i =>
                    i.FirstName.Contains(searchTerm) ||
                    i.LastName.Contains(searchTerm) ||
                    i.Email.Contains(searchTerm) ||
                    i.PhoneNumber.Contains(searchTerm) ||
                    i.Purpose.Contains(searchTerm) ||
                    i.Description.Contains(searchTerm)
                );
            }

            // Apply month/year filters
            if (selectedMonth.HasValue)
                query = query.Where(i => i.CreatedAt.Month == selectedMonth.Value);

            if (selectedYear.HasValue)
                query = query.Where(i => i.CreatedAt.Year == selectedYear.Value);

            // Order by latest first
            query = query.OrderByDescending(i => i.CreatedAt);

            // Convert to paged list (synchronous)
            var pagedList = query.ToPagedList(page, pageSize);

            // Pass filter values to View
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            return View(pagedList);
        }


        public async Task<IActionResult> Details(
     int id,
     string searchTerm,
     int? selectedMonth,
     int? selectedYear,
     int page = 1
 )
        {
            var item = await _context.PublicInquiries.FindAsync(id);
            if (item == null) return NotFound();

            // Pass current filters to the View
            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.CurrentPage = page;

            return View(item);
        }



        [HttpGet]
        public async Task<IActionResult> Reply(
            int id,
            string searchTerm,
            int? selectedMonth,
            int? selectedYear,
            int page = 1
        )
        {
            var item = await _context.PublicInquiries.FindAsync(id);
            if (item == null) return NotFound();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            ViewBag.CurrentPage = page;

            return View(item);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(
    int id,
    string response,
    string searchTerm,
    int? selectedMonth,
    int? selectedYear,
    int page = 1
)
        {
            var inquiry = await _context.PublicInquiries.FindAsync(id);
            if (inquiry == null) return NotFound();

            // Send email (your existing code)
            var subject = "Response to your inquiry";
            var body = $@"
        <p>Hello {inquiry.FirstName},</p>
        <p>{System.Net.WebUtility.HtmlEncode(response).Replace("\n", "<br/>")}</p>
        <p>Regards,<br/>Santa Fe Water System</p>";
            await _emailSender.SendEmailAsync(inquiry.Email!, subject, body);

            // Update inquiry status
            inquiry.AdminResponse = response;
            inquiry.Status = "Replied";
            inquiry.RepliedAt = DateTime.UtcNow;
            _context.Update(inquiry);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Response sent successfully!";

            // Redirect back to Index with the same filter/page
            return RedirectToAction(nameof(Index), new
            {
                searchTerm,
                selectedMonth,
                selectedYear,
                page
            });
        }
    }
}
