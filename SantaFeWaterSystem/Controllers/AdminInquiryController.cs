using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Threading.Tasks;

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

        public async Task<IActionResult> Index()
        {
            var list = await _context.PublicInquiries
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var item = await _context.PublicInquiries.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpGet]
        public async Task<IActionResult> Reply(int id)
        {
            var item = await _context.PublicInquiries.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, string response)
        {
            var inquiry = await _context.PublicInquiries.FindAsync(id);
            if (inquiry == null) return NotFound();

            // Build HTML email body (you can style this)
            var subject = "Response to your inquiry";
            var body = $@"
                <p>Hello {inquiry.FirstName},</p>
                <p>{System.Net.WebUtility.HtmlEncode(response).Replace("\n", "<br/>")}</p>
                <p>Regards,<br/>Santa Fe Water System</p>";

            // Send email via your SmtpEmailSender (reads settings from DB)
            await _emailSender.SendEmailAsync(inquiry.Email!, subject, body);

            // Update inquiry status
            inquiry.AdminResponse = response;
            inquiry.Status = "Replied";
            inquiry.RepliedAt = DateTime.UtcNow;

            _context.Update(inquiry);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Response sent successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}
