using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class UserProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly AuditLogService _audit;
        private readonly IPasswordHasher<User> _passwordHasher;

        public UserProfileController(ApplicationDbContext context, IWebHostEnvironment environment, AuditLogService audit, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _environment = environment;
            _audit = audit;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        public IActionResult Profile()
        {
            var userId = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = _context.Users.FirstOrDefault(u => u.Id == int.Parse(userId));
            if (user == null) return NotFound();

            var viewModel = new UserAdminProfileViewModel
            {
                Id = user.Id,
                Username = user.Username ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                IsMfaEnabled = user.IsMfaEnabled,
                Role = user.Role ?? string.Empty,
                ExistingProfilePicture = user.ProfileImageUrl ?? "default.png",
                IsAdmin = user.Role == "Admin"
            };

            // ✅ Set these so layout shows correct image and name
            ViewBag.ProfileImage = Url.Content($"~/images/profiles/{user.ProfileImageUrl ?? "default-avatar.png"}");
            ViewBag.FullName = user.FullName;

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserAdminProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the form and try again.";
                return View(model);
            }

            var user = _context.Users.FirstOrDefault(u => u.Id == model.Id);
            if (user == null) return NotFound();

            // ✅ Handle profile image upload
            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                try
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "profiles");
                    Directory.CreateDirectory(uploadsFolder); // Ensure it exists

                    // Delete old image if it exists and not default
                    if (!string.IsNullOrEmpty(user.ProfileImageUrl) && user.ProfileImageUrl != "default.png")
                    {
                        var oldPath = Path.Combine(uploadsFolder, user.ProfileImageUrl);
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    // Save new image
                    var extension = Path.GetExtension(model.ProfileImage.FileName);
                    var fileName = $"user_{user.Id}_{Guid.NewGuid()}{extension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProfileImage.CopyToAsync(stream);
                    }

                    user.ProfileImageUrl = fileName;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Image upload failed. " + ex.Message);
                    return View(model);
                }
            }

            // ✅ Update other profile fields
            user.FullName = model.FullName;
            user.IsMfaEnabled = model.IsMfaEnabled;

            try
            {
                _context.Update(user);
                await _context.SaveChangesAsync();

                // ✅ AUDIT LOG
                await _audit.LogAsync(user.Username, "Update Profile", $"User updated their profile. Name: {user.FullName}");


                TempData["Message"] = "Profile updated successfully.";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Saving failed. " + ex.Message);
                return View(model);
            }

            // ✅ Ensure layout has updated image and name right after redirect
            ViewBag.ProfileImage = Url.Content($"~/images/profiles/{user.ProfileImageUrl ?? "default-avatar.png"}");
            ViewBag.FullName = user.FullName;

            return RedirectToAction("Profile");
        }




        /////////////CHANGES PASSWORD///////////////
        [Authorize(Roles = "Admin,Staff")]
        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = User.FindFirstValue("UserId");
            if (!int.TryParse(userId, out int id)) return Unauthorized();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            await _context.SaveChangesAsync();

            // ✅ Audit trail
            var performedBy = User.Identity?.Name ?? "Unknown";
            var details = $"Password reset for user: {user.Username} (ID: {user.Id})";

            var audit = new AuditTrail
            {
                Action = "Reset Password",
                PerformedBy = performedBy,
                Timestamp = DateTime.UtcNow,
                Details = details
            };
            _context.AuditTrails.Add(audit);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Your password has been reset successfully.";
            return RedirectToAction("Profile");
        }
    }
}
