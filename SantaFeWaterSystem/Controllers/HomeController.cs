using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Hubs;
using SantaFeWaterSystem.Models;
using System.Diagnostics;

namespace SantaFeWaterSystem.Controllers
{
    public class HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IHubContext<VisitorHub> hub) : Controller
    {
        private readonly ILogger<HomeController> _logger = logger;
        private readonly ApplicationDbContext _context = context;
        private readonly IHubContext<VisitorHub> _hub = hub;
 



        // ================== INDEX HOMEPAGE (Public) ==================
        public async Task<IActionResult> Index()
        {
            // 1) Track visitor
            var ip = GetClientIp(HttpContext) ?? "Unknown";
            var tz = PhilippineTimeZone();
            var nowPh = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var today = nowPh.Date;

            bool already = await _context.VisitorLogs.AnyAsync(v => v.IpAddress == ip && v.VisitDateLocal == today);

            if (!already)
            {
                _context.VisitorLogs.Add(new VisitorLog
                {
                    IpAddress = ip,
                    VisitDateLocal = today,
                    VisitedAtUtc = DateTime.UtcNow
                });

                try
                {
                    await _context.SaveChangesAsync();

                    // push real-time updates for Admin/Staff
                    var counts = ComputeCounts(nowPh);
                    await _hub.Clients.All.SendAsync("ReceiveVisitorUpdate", counts);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Duplicate visitor insert prevented.");
                }
            }

            // 2) Fetch homepage content (admin editable)
            var content = await _context.HomePageContents
                                .OrderByDescending(h => h.Id)
                                .FirstOrDefaultAsync();

            // 3) fallback content if admin hasn't created any
            content ??= new HomePageContent
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

            return View(content);
        }

        // ================== Helper: Compute Counts ==================
        private object ComputeCounts(DateTime nowPh)
        {
            var today = nowPh.Date;
            var monthStart = new DateTime(nowPh.Year, nowPh.Month, 1);
            var yearStart = new DateTime(nowPh.Year, 1, 1);

            var daily = _context.VisitorLogs.Count(v => v.VisitDateLocal == today);
            var monthly = _context.VisitorLogs.Count(v => v.VisitDateLocal >= monthStart && v.VisitDateLocal < monthStart.AddMonths(1));
            var yearly = _context.VisitorLogs.Count(v => v.VisitDateLocal >= yearStart && v.VisitDateLocal < yearStart.AddYears(1));

            return new { daily, monthly, yearly };
        }

        // ================== Helpers ==================
        private static TimeZoneInfo PhilippineTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
            catch { return TimeZoneInfo.CreateCustomTimeZone("PH", TimeSpan.FromHours(8), "Philippine Time", "Philippine Time"); }
        }

        private static string? GetClientIp(HttpContext ctx)
        {
            if (ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIp))
                return cfIp.ToString();

            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var xff))
                return xff.ToString().Split(',').FirstOrDefault()?.Trim();

            return ctx.Connection.RemoteIpAddress?.ToString();
        }
    


//================== PRIVACY POLICY  ==================
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



        //================== CONTACT HANDLER DISPLAY LATEST UPDATE BY ADMIN  ==================
        public async Task<IActionResult> Contact()
        {
            // Get the first contact info (if multiple exist)
            var contact = await _context.ContactInfos.FirstOrDefaultAsync();

            
                // fallback default
                contact ??= new ContactInfo
                {
                    Phone = "(032) 123-4567",
                    Email = "support@santafewater.com",
                    FacebookUrl = "https://www.facebook.com/SantaFeWaterSystem",
                    FacebookName = "Santa Fe Water System",
                    IntroText = "For general inquiries or assistance, feel free to reach out to us via phone, email, or Facebook.",
                    WaterMeterHeading = "Water Meter Installation",
                    WaterMeterInstructions = "If you would like to apply for a water meter connection, please visit the Santa Fe Municipal Hall located in Bantayan Island, Cebu. You may also call us first to learn about the required documents and qualifications before visiting."
                };           

            return View(contact);
        }
    }
}
