using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    public class InquiryController : Controller
    {
        private readonly ApplicationDbContext _context;
        public InquiryController(ApplicationDbContext context) => _context = context;

        // GET: /Inquiry/Create
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Create() => View();

        // POST: /Inquiry/Create
        [HttpPost]
        // POST: /Inquiry/Create
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PublicInquiry model)
        {
            // Remove validation for IsAgreed so it won’t block submission
            ModelState.Remove(nameof(PublicInquiry.IsAgreed));

            if (!ModelState.IsValid)
                return View(model);

            _context.PublicInquiries.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your inquiry has been submitted successfully!";
            return RedirectToAction(nameof(Create));
        }
    }
}
