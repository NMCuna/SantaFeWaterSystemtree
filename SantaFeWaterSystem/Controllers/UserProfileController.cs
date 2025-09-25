using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class UserProfileController(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        AuditLogService audit,
        IPasswordHasher<User> passwordHasher,
        PasswordPolicyService passwordPolicyService
    ) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _environment = environment;
        private readonly AuditLogService _audit = audit;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly PasswordPolicyService _passwordPolicyService = passwordPolicyService;


        /////////////////////////////////
        //    PROFILE ADMIN,STAFF      //
        /////////////////////////////////



        // ================== PROFILE MANAGEMENT VIEW ==================

        // GET: UserProfile
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

            // Set these so layout shows correct image and name
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

            // Handle profile image upload
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

            // Update other profile fields
            user.FullName = model.FullName;
            user.IsMfaEnabled = model.IsMfaEnabled;

            try
            {
                _context.Update(user);
                await _context.SaveChangesAsync();

                // AUDIT LOG             
                var username = user?.Username ?? "Unknown";

                await _audit.LogAsync(action: "Update Profile", details: $"User updated their profile. Name: {user?.FullName ?? "N/A"}", performedBy: username);

                TempData["Message"] = "Profile updated successfully.";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Saving failed. " + ex.Message);
                return View(model);
            }

            // Ensure layout has updated image and name right after redirect
            ViewBag.ProfileImage = Url.Content($"~/images/profiles/{user?.ProfileImageUrl ?? "default-avatar.png"}");
            ViewBag.FullName = user?.FullName ?? "Unknown User";

            return RedirectToAction("Profile");
        }




        // ================== RESET PASSWORD FOR ADMIN/STAFF ==================

        // GET: Show Reset Password View
        [HttpGet]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> ResetPassword()
        {
            // 🔹 Load latest password policy
            var policy = await _context.PasswordPolicies
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (policy == null)
            {
                policy = new PasswordPolicy
                {
                    MinPasswordLength = 8,
                    RequireComplexity = true,
                    PasswordHistoryCount = 5,
                    MaxPasswordAgeDays = 0,
                    MinPasswordAgeDays = 1
                };
            }

            ViewBag.PasswordPolicy = policy;
            return View(new ResetPasswordViewModel());
        }

        // POST: Process Reset Password
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            // 🔹 Load latest password policy
            var policy = await _context.PasswordPolicies
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            ViewBag.PasswordPolicy = policy ?? new PasswordPolicy
            {
                MinPasswordLength = 8,
                RequireComplexity = true,
                PasswordHistoryCount = 5
            };

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "❌ Please correct the errors below.";
                return View(model);
            }

            var userId = User.FindFirstValue("UserId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!int.TryParse(userId, out int id))
                return Unauthorized();

            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound();

            // 🔹 Verify current password
            bool isCurrentPasswordValid = false;
            try
            {
                isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // Optionally log
            }

            if (!isCurrentPasswordValid)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                TempData["Error"] = "❌ Current password is incorrect.";
                return View(model);
            }

            // 🔹 Validate new password against policy
            var isValidPassword = await _passwordPolicyService.ValidatePasswordAsync(user.Id, model.NewPassword);
            if (!isValidPassword)
            {
                ModelState.AddModelError(nameof(model.NewPassword),
                    "Password must meet all requirements (length, complexity, history).");
                TempData["Error"] = "❌ Change password failed — new password does not meet the requirements.";
                return View(model);
            }

            // 🔹 Hash and save new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);

            // 🔹 Save password history
            await _passwordPolicyService.SavePasswordHistoryAsync(user.Id, user.PasswordHash);

            // 🔹 Log audit action
            _context.AuditTrails.Add(new AuditTrail
            {
                Action = "Reset Password",
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Timestamp = DateTime.UtcNow,
                Details = $"Admin/Staff '{user.Username}' reset their own password."
            });

            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ Password changed successfully.";
            return RedirectToAction("Profile");
        }

    }
}
