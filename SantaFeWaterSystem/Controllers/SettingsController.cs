using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class SettingsController : BaseController
    {
        private readonly IWebHostEnvironment _env;

        public SettingsController(
            IWebHostEnvironment env,
            PermissionService permissionService,
            ApplicationDbContext context,
            AuditLogService audit)
            : base(permissionService, context, audit)
        {
            _env = env;
        }

        [HttpGet]
        public IActionResult QrCodes()
        {
            string gcashPath = "/images/gcash-qr.png";
            string mayaPath = "/images/maya-qr.png";

            string gcashPhysical = Path.Combine(_env.WebRootPath, "images", "gcash-qr.png");
            string mayaPhysical = Path.Combine(_env.WebRootPath, "images", "maya-qr.png");

            ViewBag.GcashImagePath = System.IO.File.Exists(gcashPhysical) ? $"{gcashPath}?v={System.Guid.NewGuid()}" : null;
            ViewBag.MayaImagePath = System.IO.File.Exists(mayaPhysical) ? $"{mayaPath}?v={System.Guid.NewGuid()}" : null;

            return View();
        }

        // ✅ GCash QR Upload
        [HttpPost]
        public async Task<IActionResult> UploadGcashQr(IFormFile qrImage)
        {
            return await SaveQrImageAsync(qrImage, "gcash-qr.png", "GCash");
        }
        // ✅ Maya QR Upload
        [HttpPost]
        public async Task<IActionResult> UploadMayaQr(IFormFile qrImage)
        {
            return await SaveQrImageAsync(qrImage, "maya-qr.png", "Maya");
        }

        // ✅ GCash QR Delete
        [HttpPost]
        public async Task<IActionResult> DeleteGcashQr()
        {
            return await DeleteQrImageAsync("gcash-qr.png", "GCash");
        }

        // ✅ Maya QR Delete
        [HttpPost]
        public async Task<IActionResult> DeleteMayaQr()
        {
            return await DeleteQrImageAsync("maya-qr.png", "Maya");
        }

        private async Task<IActionResult> SaveQrImageAsync(IFormFile qrImage, string fileName, string label)
        {
            if (qrImage != null && qrImage.Length > 0)
            {
                var supportedTypes = new[] { "image/png", "image/jpeg" };
                if (!supportedTypes.Contains(qrImage.ContentType))
                {
                    TempData["Message"] = $"Invalid file type for {label}. Only PNG or JPEG allowed.";
                    return RedirectToAction("QrCodes");
                }

                string folder = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, fileName);

                // Delete old image if exists
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await qrImage.CopyToAsync(stream);
                }

                // ✅ Audit trail
                var performedBy = User?.Identity?.Name ?? "System";
                if (performedBy.Length > 100) performedBy = performedBy.Substring(0, 100);

                var audit = new AuditTrail
                {
                    Action = "Upload QR Code",
                    PerformedBy = performedBy,
                    Timestamp = DateTime.UtcNow,
                    Details = $"{label} QR Code uploaded: {fileName}"
                };

                try
                {
                    _context.AuditTrails.Add(audit);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Optional: log error
                    Console.WriteLine($"Audit trail failed: {ex.Message}");
                }

                TempData["Message"] = $"{label} QR Code uploaded successfully.";
            }
            else
            {
                TempData["Message"] = $"Please select a file to upload for {label}.";
            }

            return RedirectToAction("QrCodes");
        }

        private async Task<IActionResult> DeleteQrImageAsync(string fileName, string label)
        {
            string filePath = Path.Combine(_env.WebRootPath, "images", fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);

                // ✅ Audit trail
                var performedBy = User?.Identity?.Name ?? "System";
                if (performedBy.Length > 100) performedBy = performedBy.Substring(0, 100);

                var audit = new AuditTrail
                {
                    Action = "Delete QR Code",
                    PerformedBy = performedBy,
                    Timestamp = DateTime.UtcNow,
                    Details = $"{label} QR Code deleted: {fileName}"
                };

                try
                {
                    _context.AuditTrails.Add(audit);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Optional: log the error somewhere
                    Console.WriteLine($"Audit trail failed: {ex.Message}");
                }

                TempData["Message"] = $"{label} QR Code deleted successfully.";
            }
            else
            {
                TempData["Message"] = $"{label} QR Code not found.";
            }

            return RedirectToAction("QrCodes");
        }

    }
}
