using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Diagnostics;

namespace SantaFeWaterSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        protected readonly ApplicationDbContext _context;
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch the latest home page content created by admin
            var content = await _context.HomePageContents
                                .OrderByDescending(h => h.Id)
                                .FirstOrDefaultAsync();

            if (content == null)
            {
                // Optional: fallback content if admin hasn't created any
                content = new HomePageContent
                {
                    Title = "Welcome to Santa Fe Water Billing System",
                    Subtitle = "Manage your water bills, view payment history, and stay connected—anytime, anywhere.",
                    Card1Title = "View Your Bills",
                    Card1Text = "Track your monthly water consumption and billing statements with ease.",
                    Card1Icon = "bi-droplet-half",
                    Card2Title = "Make Payments",
                    Card2Text = "Submit payments online and keep a record of your payment history.",
                    Card2Icon = "bi-credit-card-2-front",
                    Card3Title = "Update Profile",
                    Card3Text = "Keep your contact information up to date to ensure seamless communication.",
                    Card3Icon = "bi-person-lines-fill"
                };
            }

            return View(content);
        }

        public IActionResult Privacy()
        {
            var latestPolicy = _context.PrivacyPolicies
                .Include(p => p.Sections)
                .OrderByDescending(p => p.Version)
                .FirstOrDefault();

            if (latestPolicy == null)
            {
                return NotFound();
            }

            return View(latestPolicy);
        }




        public async Task<IActionResult> Contact()
        {
            // Get the first contact info (if multiple exist)
            var contact = await _context.ContactInfos.FirstOrDefaultAsync();
            if (contact == null)
            {
                // fallback default
                contact = new ContactInfo
                {
                    Phone = "(032) 123-4567",
                    Email = "support@santafewater.com",
                    FacebookUrl = "https://www.facebook.com/SantaFeWaterSystem",
                    FacebookName = "Santa Fe Water System",
                    IntroText = "For general inquiries or assistance, feel free to reach out to us via phone, email, or Facebook.",
                    WaterMeterHeading = "Water Meter Installation",
                    WaterMeterInstructions = "If you would like to apply for a water meter connection, please visit the Santa Fe Municipal Hall located in Bantayan Island, Cebu. You may also call us first to learn about the required documents and qualifications before visiting."
                };
            }

            return View(contact);
        }
    }
}
