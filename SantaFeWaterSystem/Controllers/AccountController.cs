using Microsoft.AspNetCore.Mvc;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Data;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using OtpNet;
using QRCoder;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using SantaFeWaterSystem.Services;
using System.Security.Cryptography;
using System.Text;

namespace SantaFeWaterSystem.Controllers;

public class AccountController(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    IEmailSender emailSender,
    AuditLogService audit,
    IConfiguration configuration
) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly AuditLogService _audit = audit;
    private readonly IConfiguration _configuration = configuration;



    ///////////////////////////
    /////ADMIN/STAFF LOGIN ////
    ///////////////////////////
   
    [HttpGet]
        public IActionResult AdminLogin(string token)
        {
            // ✅ Load token from config file
            var requiredToken = _configuration["AdminAccess:LoginViewToken"];

            // ✅ Validate the token from query string
            if (string.IsNullOrEmpty(token) || token != requiredToken)
            {
                TempData["Error"] = "Unauthorized access.";
                return RedirectToAction("AccessDenied", "Account"); // Or return Unauthorized();
            }

            return View(new AdminLoginViewModel());
        }
        public IActionResult AccessDenied()
        {
            return View(); // Show an error message or redirect elsewhere
        }


  
    [HttpPost]
        public async Task<IActionResult> AdminLogin(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == model.Username && (u.Role == "Admin" || u.Role == "Staff"));

            if (user == null)
            {
                await _audit.LogAsync("Failed Login", $"Unknown user attempted login with username: {model.Username}", model.Username);
                ModelState.AddModelError("", "Invalid username or password.");
                return View(model);
            }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var unlockTimePH = TimeZoneInfo.ConvertTimeFromUtc(user.LockoutEnd.Value, phTimeZone);

            ViewBag.UnlockTime = unlockTimePH.ToString("yyyy-MM-ddTHH:mm:ss"); // JS-friendly ISO format
            ViewBag.UnlockTimeDisplay = unlockTimePH.ToString("f"); // e.g., Monday, July 15, 2025 8:45 PM
            ViewBag.Role = user.Role; // ✅ Pass role to view

            await _audit.LogAsync("Locked Out", $"Login attempt while locked out - Role: {user.Role}, Username: {user.Username}", user.Username);

            return View("TemporaryLocked", user); // 👈 Show lockout page
        }


        bool passwordValid;
            try
            {
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
                passwordValid = result == PasswordVerificationResult.Success;
            }
            catch
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
            }

            if (!passwordValid)
            {
                user.AccessFailedCount++;

                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    await _audit.LogAsync("Account Locked", $"User {user.Username} locked out after 5 failed attempts.", user.Username);
                    ModelState.AddModelError("", "Account locked due to too many failed attempts. Try again after 15 minutes.");
                }
                else
                {
                    await _audit.LogAsync("Failed Login", $"Invalid password attempt for user: {user.Username}", user.Username);
                    ModelState.AddModelError("", "Invalid username or password.");
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return View(model);
            }

            // ✅ Password valid — reset failed count and lockout
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ✅ Store user ID in session for 2FA
            HttpContext.Session.SetInt32("2FA_UserId", user.Id);
            HttpContext.Session.SetString("2FA_Expiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));


            // Log login before redirecting to 2FA
            await _audit.LogAsync("Login Success", $"User {user.Username} passed login and redirected to 2FA.", user.Username);

            // ✅ Go to Setup or Verification
            if (!user.IsMfaEnabled)
                return RedirectToAction("Setup2FAAdmin", "Account");

            return RedirectToAction("Verify2FAAdmin", "Account");
        }





    // GET: /Account/RegisterAdmin
    [Authorize(Roles = "Admin")]
    [HttpGet]
        public IActionResult RegisterAdmin() => View();


    [Authorize(Roles = "Admin")]
    [HttpPost]
        public IActionResult RegisterAdmin(string fullname, string username, string password, string role)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(fullname) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(role))
            {
                ViewBag.Error = "Please fill in all required fields.";
                return View();
            }

            // Normalize username for consistent check
            var normalizedUsername = username.Trim().ToLower();

            // Check if username exists (case-insensitive)
            bool usernameExists = _context.Users.Any(u => u.Username.ToLower() == normalizedUsername);
            if (usernameExists)
            {
                ViewBag.Error = "Username already exists.";
                return View();
            }

            var user = new User
            {
                FullName = fullname.Trim(),
                Username = username.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role.Trim()
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            // ✅ Log audit trail
            var performedBy = User.Identity?.Name ?? "Unknown";
            var details = $"New user registered. Username: {user.Username}, Role: {user.Role}";

            var audit = new AuditTrail
            {
                Action = "Register Admin/Staff",
                PerformedBy = performedBy,
                Timestamp = DateTime.Now,
                Details = details
            };

            _context.AuditTrails.Add(audit);
            _context.SaveChanges();

            TempData["Message"] = "Admin/Staff registered successfully.";
            return RedirectToAction("ManageUsers", "Admin", new { roleFilter = "Admin" });
        }



    // GET: /Account/RegisterUser
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
        public IActionResult RegisterUser()
        {
            return View(new UserRegisterViewModel());
        }


    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
        public async Task<IActionResult> RegisterUser(UserRegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "All fields are required.";
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.AccountNumber == model.AccountNumber))
            {
                ViewBag.Error = "Account number already exists.";
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                ViewBag.Error = "Username already exists.";
                return View(model);
            }

            var user = new User
            {
                AccountNumber = model.AccountNumber,
                Username = model.Username,
                Role = "User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                AccessFailedCount = 0,
                IsMfaEnabled = false
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // ✅ Log audit trail
            var performedBy = User.Identity?.Name ?? "Unknown";
            var audit = new AuditTrail
            {
                Action = "Register User",
                PerformedBy = performedBy,
                Timestamp = DateTime.Now,
                Details = $"Registered new user with username '{user.Username}' and account number '{user.AccountNumber}'."
            };
            _context.AuditTrails.Add(audit);
            await _context.SaveChangesAsync();

            TempData["Message"] = "User registered successfully!";
            return RedirectToAction("ManageUsers", "Admin", new { roleFilter = "User" });
        }















        ///////////////////////////
        /////Account/UserLogin////
        ///////////////////////////

        // GET: /Account/UserLogin

        [HttpGet]
        public IActionResult UserLogin()
        {
            return View(new UserLoginViewModel());  // pass empty model to ensure no values
        }


        // POST: /Account/UserLogin
        [HttpPost]
        public async Task<IActionResult> UserLogin(UserLoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountNumber == model.AccountNumber && u.Role == "User");

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid account number or password.");
                await _audit.LogAsync("Failed Login", "Failed login attempt: account not found or incorrect role.", model.AccountNumber ?? "Unknown");
                return View(model);
            }
       //lockuser///
        if (user.IsLocked)
        {
           
            await _audit.LogAsync("Login Blocked", "Login blocked due to admin-locked account.", user.AccountNumber);
            return View("AccountLocked", user); // pass the user to a custom view

        }
        // Lockout: 5 failed login attempts
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var unlockTimePH = TimeZoneInfo.ConvertTimeFromUtc(user.LockoutEnd.Value, phTimeZone);

            ViewBag.UnlockTime = unlockTimePH.ToString("yyyy-MM-ddTHH:mm:ss"); // For countdown timer (JS)
            ViewBag.UnlockTimeDisplay = unlockTimePH.ToString("f"); // Human-readable (e.g. July 16, 2025 8:00 PM)
            ViewBag.Role = user.Role; // ✅ Pass user role to view

            await _audit.LogAsync("Locked Out", $"Login attempt blocked due to lockout. Try again at {unlockTimePH:f} (PH time).", user.AccountNumber);

            return View("TemporaryLocked", user); //  View handles display + countdown
        }


        bool passwordValid;
            try
            {
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
                passwordValid = result == PasswordVerificationResult.Success;
            }
            catch
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
            }

            if (!passwordValid)
            {
                user.AccessFailedCount++;

                string detail = $"Failed login for user {user.AccountNumber}. Attempt #{user.AccessFailedCount}.";

                if (user.AccessFailedCount >= 5)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                    detail += " Account locked for 15 minutes.";
                    ModelState.AddModelError("", "Account locked due to too many failed attempts. Try again after 15 minutes.");
                }
                else
                {
                    ModelState.AddModelError("", "Invalid account number or password.");
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                await _audit.LogAsync("Failed Login", detail, user.AccountNumber);
                return View(model);
            }

            // ✅ Reset failed count
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            if (user.IsMfaEnabled)
            {
                HttpContext.Session.SetInt32("2FA_UserId", user.Id);
                await _audit.LogAsync("2FA Login Initiated", "User login passed password check. Redirected to 2FA.", user.AccountNumber);
                return RedirectToAction("Verify2FAUser");
            }

            // ✅ Sign in
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.AccountNumber ?? user.Username ?? ""),
        new Claim(ClaimTypes.Role, user.Role),
        new Claim("UserId", user.Id.ToString())
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        var consumer = await _context.Consumers
       .FirstOrDefaultAsync(c => c.UserId == user.Id);

        if (consumer != null)
        {
            var latestPolicy = await _context.PrivacyPolicies
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync();

            if (latestPolicy != null)
            {
                var agreement = await _context.UserPrivacyAgreements
                    .Where(a => a.ConsumerId == consumer.Id)
                    .OrderByDescending(a => a.PolicyVersion)
                    .FirstOrDefaultAsync();

                if (agreement == null || agreement.PolicyVersion < latestPolicy.Version)
                {
                    await _audit.LogAsync("Privacy Agreement Required", "User redirected to agree to latest privacy policy.", user.AccountNumber);
                    return RedirectToAction("Agree", "Privacy", new { version = latestPolicy.Version });
                }
            }
        }

        await _audit.LogAsync("Login Success", "User logged in successfully.", user.AccountNumber);
            return RedirectToAction("Dashboard", "User");
        }







        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Get the currently logged-in user's name or ID for audit
            var username = User.Identity?.Name ?? "Unknown";

            // Sign out and clear auth cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clear session
            HttpContext.Session.Clear();

            // ✅ Log logout action via service
            await _audit.LogAsync("Logout", "User logged out successfully.", username);

            // Redirect to homepage or login
            return RedirectToAction("Index", "Home");
        }















    ////////////User Consumer in Manage user////////////////////
    // GET: Admin/ResetPassword/5
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
        public IActionResult ResetPassword(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == id);
            if (user == null)
                return NotFound();

            return View("ResetPassword", user); // Loads ResetPassword.cshtml with User model
        }

    // POST: Admin/ResetPassword/5
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string NewPassword, string ConfirmPassword)
        {
            if (NewPassword != ConfirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                    return NotFound();

                return View(user);
            }

            var userToReset = await _context.Users.FindAsync(id);
            if (userToReset == null)
                return NotFound();

            // ✅ Hash the new password
            userToReset.PasswordHash = _passwordHasher.HashPassword(userToReset, NewPassword);

            await _context.SaveChangesAsync();

            // ✅ Add AuditTrail
            _context.AuditTrails.Add(new AuditTrail
            {
                Action = "Reset Password",
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Timestamp = DateTime.Now,
                Details = $"Password for user '{GetUserIdentifier(userToReset)}' was reset."
            });

            await _context.SaveChangesAsync();

            TempData["Message"] = $"Password for {GetUserIdentifier(userToReset)} has been reset successfully.";

            // Redirect based on role
            if (userToReset.Role == "Admin" || userToReset.Role == "Staff")
            {
                return RedirectToAction("ManageUsers", "Admin", new { roleFilter = "Admin" });
            }
            else if (userToReset.Role == "User")
            {
                return RedirectToAction("ManageUsers", "Admin", new { roleFilter = "User" });
            }
            else
            {
                return RedirectToAction("ManageUsers", "Admin");
            }
        }


        // Utility: get identifier string for message
        private string GetUserIdentifier(User user)
        {
            if (user.Role == "User")
                return user.AccountNumber ?? "Unknown Account Number";
            else
                return user.Username ?? "Unknown Username";
        }

        // Hash password (replace with your real hashing)
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

















    ////////////User Consumer in  User in Login Forgot pass////////////////////
    // GET: /Account/ForgotPasswordUser
    [HttpGet]
    public IActionResult ForgotPasswordUser()
    {
        return View();
    }

    // POST: /Account/ForgotPasswordUser
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPasswordUser(ForgotPasswordUserViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Step 1: Find the Consumer by email
        var consumer = await _context.Consumers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Email == model.Email);

        if (consumer == null || consumer.User == null)
        {
            // Always redirect regardless of match to avoid exposing user info
            return RedirectToAction(nameof(ForgotPasswordUserConfirmation));
        }

        // Step 2: Generate token and set expiry on User
        string token = GenerateSecureToken();
        consumer.User.PasswordResetToken = token;
        consumer.User.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);

        await _context.SaveChangesAsync();

        // Step 3: Generate reset link with token
        var resetLink = Url.Action(
            "ResetPasswordUser",
            "Account",
            new { token },
            Request.Scheme);

        // Step 4: Build styled email body
        string emailBody = $@"
        <h2 style='color: #007bff; font-family: Arial, sans-serif;'>Santa Fe Water Billing System</h2>
        <p>Please reset your password by clicking the button below:</p>
        <p>
            <a href='{resetLink}' 
               style='
                   display: inline-block; 
                   padding: 12px 24px; 
                   font-size: 16px; 
                   color: white; 
                   background-color: #007bff; 
                   text-decoration: none; 
                   border-radius: 6px;
                   font-weight: bold;
               '>
                Reset Password
            </a>
        </p>
        <p style='font-size: 12px; color: #555;'>If you did not request this, please ignore this email.</p>
    ";

        // Step 5: Send email
        await _emailSender.SendEmailAsync(consumer.Email, "Reset Password - Santa Fe Water Billing System", emailBody);

        // ✅ AuditTrail
        _context.AuditTrails.Add(new AuditTrail
        {
            Action = "Forgot Password Request",
            PerformedBy = consumer.User.Username ?? consumer.Email,
            Timestamp = DateTime.Now,
            Details = $"Password reset token generated and sent to email: {consumer.Email}"
        });

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ForgotPasswordUserConfirmation));
    }




    // GET: /Account/ForgotPasswordUserConfirmation
    [HttpGet]
        public IActionResult ForgotPasswordUserConfirmation()
        {
            return View();
        }

        private string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            // Remove characters not URL-safe
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
        }

        // GET: /Account/ResetPasswordUser?token=xyz
        [HttpGet]
        public async Task<IActionResult> ResetPasswordUser(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("A token must be supplied for password reset.");

            // Find the user by token and check expiry
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token && u.PasswordResetExpiry > DateTime.UtcNow);

            if (user == null)
            {
                return View("ResetPasswordUserInvalid"); // Create this view to show invalid or expired token
            }

            var model = new ResetPasswordUserViewModel { Token = token };
            return View(model);
        }






    ////////////User Consumer in Profile user////////////////////
    // POST: /Account/ResetPasswordUser
    [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordUser(ResetPasswordUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PasswordResetToken == model.Token && u.PasswordResetExpiry > DateTime.UtcNow);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired token.");
                return View(model);
            }

            // Hash the new password
            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);

            // Clear token and expiry
            user.PasswordResetToken = null;
            user.PasswordResetExpiry = null;

            await _context.SaveChangesAsync();

            // ✅ Add Audit Trail
            _context.AuditTrails.Add(new AuditTrail
            {
                Action = "Reset Password (User)",
                PerformedBy = user.Username ?? $"UserId:{user.Id}",
                Timestamp = DateTime.Now,
                Details = $"Password reset successfully via token-based recovery."
            });

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ResetPasswordUserConfirmation));
        }


        // GET: /Account/ResetPasswordUserConfirmation
        [HttpGet]
        public IActionResult ResetPasswordUserConfirmation()
        {
            return View();
        }







        //HELPER FOR AUDITS//
        private async Task LogAudit(string action, string details)
        {
            var username = User.Identity?.IsAuthenticated == true
                ? User.Identity.Name
                : "Anonymous";

            var audit = new AuditTrail
            {
                Action = action,
                Details = details,
                PerformedBy = username,
                Timestamp = DateTime.Now
            };

            _context.AuditTrails.Add(audit);
            await _context.SaveChangesAsync();
        }



















        ///////////////////////////
        /////Account/ADMIN2FA////
        ///////////////////////////


        [HttpGet]
        public async Task<IActionResult> Setup2FAAdmin()
        {
            var userId = HttpContext.Session.GetInt32("2FA_UserId");
            if (userId == null)
                return RedirectToAction("AdminLogin");

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null || (user.Role != "Admin" && user.Role != "Staff"))
                return Unauthorized();

            var (secret, qrCodeBase64) = TwoFactorHelper.GenerateSetupCode(user.Username!, "SantaFeWaterSystem");

            return View(new TwoFactorSetupViewModel
            {
                UserId = user.Id,
                Role = user.Role,
                SecretKey = secret,
                QRCodeUrl = $"data:image/png;base64,{qrCodeBase64}"
            });
        }


        [HttpPost]
        public async Task<IActionResult> Setup2FAAdmin(TwoFactorSetupViewModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null || (user.Role != "Admin" && user.Role != "Staff"))
                return Unauthorized();

            if (!TwoFactorHelper.VerifyCode(model.SecretKey, model.Code))
            {
                ModelState.AddModelError("", "Invalid code. Please try again.");
                return View(model);
            }

            user.MfaSecret = model.SecretKey;
            user.IsMfaEnabled = true;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ❌ Do NOT sign in the user here
            // ✅ Keep session (2FA_UserId) active so Verify2FAAdmin can finish login

            TempData["Success"] = "2FA setup complete. Please verify your code to continue.";
            return RedirectToAction("Verify2FAAdmin", "Account");
        }




        [HttpGet]
        public IActionResult Verify2FAAdmin()
        {
            // ✅ Get the user ID from session (set during login)
            var userId = HttpContext.Session.GetInt32("2FA_UserId");

            // ✅ NEW: Get the 2FA expiry timestamp
            var expiryStr = HttpContext.Session.GetString("2FA_Expiry");

            // ✅ NEW: Check if userId or expiry is missing or expired
            if (userId == null || string.IsNullOrEmpty(expiryStr) ||
                !DateTime.TryParse(expiryStr, out var expiryTime) || DateTime.UtcNow > expiryTime)
            {
                // ✅ NEW: Cleanup session keys if expired
                HttpContext.Session.Remove("2FA_UserId");
                HttpContext.Session.Remove("2FA_Expiry");

                TempData["Error"] = "2FA session expired. Please login again.";
                return RedirectToAction("AdminLogin");
            }

            // ✅ All good, render the 2FA verification page
            return View(new TwoFactorViewModel
            {
                UserId = userId.Value,
                Role = "Admin"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FAAdmin(TwoFactorViewModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null || !user.IsMfaEnabled)
                return RedirectToAction("AdminLogin");

            if (!TwoFactorHelper.VerifyCode(user.MfaSecret!, model.Code))
            {
                ModelState.AddModelError("", "Invalid 2FA code.");
                return View(model);
            }

            // ✅ Build claims
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Username!),
        new Claim(ClaimTypes.Role, user.Role!),
        new Claim("UserId", user.Id.ToString())
    };

            // ✅ Role-based permissions
            if (user.Role == "Admin")
            {
                var adminPermissions = new List<string>
        {
            "ManageUsers", "ManageConsumers", "ManageBilling", "ManagePayments",
            "ManageDisconnection", "ManageNotifications", "ManageSupport",
            "ViewReports", "ManageFeedback", "RegisterAdmin", "RegisterUser",
            "ManageQRCodes", "ManageRate", "AuditTrail","EditUser", "ResetPassword",
            "DeleteUser", "Reset2FA", "LockUser", "UnlockUser","ViewConsumer", 
            "EditConsumer", "DeleteConsumer","ViewBilling", "EditBilling", "DeleteBilling", 
            "NotifyBilling", "ViewPenaltyLog", "ViewPayment", "EditPayment", "DeletePayment", 
            "VerifyPayment", "ManagePrivacyPolicy","ManageContact","ManageHome","ManageSystemName",
            "ManageCommunity"
        };

                foreach (var permission in adminPermissions)
                    claims.Add(new Claim("Permission", permission));
            }
            else if (user.Role == "Staff")
            {
                var permissionIds = await _context.StaffPermissions
                    .Where(sp => sp.StaffId == user.Id)
                    .Select(sp => sp.PermissionId)
                    .ToListAsync();

                var permissions = await _context.Permissions
                    .Where(p => permissionIds.Contains(p.Id))
                    .Select(p => p.Name)
                    .ToListAsync();

                foreach (var permission in permissions)
                    claims.Add(new Claim("Permission", permission));
            }

            // ✅ Sign in the user
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // ✅ Clear 2FA session temp ID
            HttpContext.Session.Remove("2FA_UserId");

            // ✅ Audit the success (use your audit service)
            await _audit.LogAsync("2FA Verified", $"Admin/Staff {user.Username} completed 2FA and signed in.", user.Username);

            TempData["Success"] = "2FA verified. Welcome!";
            return RedirectToAction("Dashboard", "Admin");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset2FAAdmin()
        {
            // ✅ Use the same claim as in login: "UserId"
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return RedirectToAction("AdminLogin");

            var user = await _context.Users.FindAsync(userId);
            if (user == null || (user.Role != "Admin" && user.Role != "Staff"))
                return Unauthorized();

            // ✅ Clear 2FA settings
            user.IsMfaEnabled = false;
            user.MfaSecret = null;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ✅ Log reset action
            await _audit.LogAsync("2FA Reset", $"Admin/Staff {user.Username} reset their 2FA settings.", user.Username);
            // ✅ Sign out for security
            await HttpContext.SignOutAsync();

            TempData["Success"] = "Two-Factor Authentication has been reset. Please log in again.";
            return RedirectToAction("AdminLogin", "Account");
        }

















        ///////////////////////////
        /////Account/USER2FA////
        ///////////////////////////

        [HttpGet]
        public async Task<IActionResult> Setup2FAUser()
        {
            var userId = int.Parse(User.FindFirst("UserId")!.Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.Role != "User")
                return Unauthorized();

            var (secret, qrCodeBase64) = TwoFactorHelper.GenerateSetupCode(user.AccountNumber!, "SantaFeWaterSystem");

            return View(new TwoFactorSetupViewModel
            {
                UserId = user.Id,
                Role = "User",
                SecretKey = secret,
                QRCodeUrl = $"data:image/png;base64,{qrCodeBase64}"
            });
        }

        [HttpPost]
        public async Task<IActionResult> Setup2FAUser(TwoFactorSetupViewModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null || user.Role != "User")
                return Unauthorized();

            if (!TwoFactorHelper.VerifyCode(model.SecretKey, model.Code))
            {
                ModelState.AddModelError("", "Invalid authentication code. Please try again.");
                return View(model);
            }

            user.MfaSecret = model.SecretKey;
            user.IsMfaEnabled = true;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // 📝 Audit using centralized service
            string performedBy = $"{user.AccountNumber} ({user.Username})";
            await _audit.LogAsync("2FA Enabled", "User enabled 2FA successfully.", performedBy);

            TempData["Success"] = "2FA has been enabled.";
            return RedirectToAction("Dashboard", "User");
        }



        [HttpGet]
        public IActionResult Verify2FAUser()
        {
            var userId = HttpContext.Session.GetInt32("2FA_UserId");
            if (userId == null) return RedirectToAction("UserLogin");

            return View(new TwoFactorViewModel { UserId = userId.Value, Role = "User" });
        }

        [HttpPost]
        public async Task<IActionResult> Verify2FAUser(TwoFactorViewModel model)
        {
            var user = await _context.Users.FindAsync(model.UserId);
            if (user == null || !user.IsMfaEnabled)
                return RedirectToAction("UserLogin");

            if (!TwoFactorHelper.VerifyCode(user.MfaSecret!, model.Code))
            {
                ModelState.AddModelError("", "Invalid authentication code.");
                return View(model);
            }

            // ✅ Sign in with claims
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.AccountNumber!),
        new Claim(ClaimTypes.Role, user.Role!),
        new Claim("UserId", user.Id.ToString())
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // ✅ Audit using central logging service
            string performedBy = $"{user.AccountNumber} ({user.Username})";
            await _audit.LogAsync("2FA Verified", "User completed 2FA and logged in successfully.", performedBy);

            TempData["Success"] = "2FA passed. Welcome!";
            return RedirectToAction("Dashboard", "User");
        }


        [HttpPost]
        public async Task<IActionResult> Reset2FAUser()
        {
            var userId = int.Parse(User.FindFirst("UserId")!.Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.Role != "User")
                return Unauthorized();

            user.IsMfaEnabled = false;
            user.MfaSecret = null;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // ✅ Audit log
            string performedBy = $"{user.AccountNumber} ({user.Username})";
            await _audit.LogAsync("2FA Reset", "User disabled 2FA on their account.", performedBy);

            TempData["Success"] = "2FA has been disabled.";
            return RedirectToAction("Dashboard", "User");
        }
















    ///////////////////////////
    /////Backup ResetPassAdmin////
    ///////////////////////////
    [HttpGet]
    [AllowAnonymous]
    public IActionResult MySecretEmergenceResetPasswordforAdmin(string token)
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(12, 0, 0); // 12 PM
        var end = new TimeSpan(17, 0, 0);   // 5 PM

        ViewBag.CurrentTime = DateTime.Now.ToString("hh:mm tt");

        var today = DateTime.Today.DayOfWeek.ToString();
        var expectedToken = _configuration[$"AdminResetTokens:{today}"];

        Console.WriteLine($"[DEBUG] Today: {today}, Expected Token: {expectedToken}, Provided: {token}");

        // Token must be valid
        if (string.IsNullOrWhiteSpace(token) || token != expectedToken)
        {
            TempData["Error"] = "⛔ Invalid or missing token. Please contact the developer.";
            return RedirectToAction("AccessDenied", "Account");
        }

        // If time is outside the allowed range, show a warning (but allow the page)
        if (now < start || now > end)
        {
            ViewBag.TimeWarning = "⏰ This page is only available between 12:00 PM and 5:00 PM.";
        }

        ViewBag.Token = token;
        TempData["ValidToken"] = token; // Save token across GET → POST
        return View();
    }



    // ✅ POST: Handle Reset Password
    [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MySecretEmergenceResetPasswordforAdmin(string NewPassword, string ConfirmPassword)
        {
            // ✅ Optional: token validation (recheck)
            string token = TempData["ValidToken"] as string; // from GET
            var today = DateTime.Today.DayOfWeek.ToString();
            var expectedToken = _configuration[$"AdminResetTokens:{today}"];

            if (string.IsNullOrWhiteSpace(token) || token != expectedToken)
            {
                TempData["Error"] = "⛔ Token expired or invalid. Please request a new one.";
                return RedirectToAction("MySecretEmergenceResetPasswordforAdmin");
            }

            if (NewPassword != ConfirmPassword)
            {
                TempData["Error"] = "❌ Passwords do not match.";
            return RedirectToAction("MySecretEmergenceResetPasswordforAdmin", new { token });
        }

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Admin");

            if (admin == null)
            {
                TempData["Error"] = "❌ Admin not found.";
            return RedirectToAction("MySecretEmergenceResetPasswordforAdmin", new { token });
        }

        // ✅ Update password securely
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            admin.PasswordResetToken = null;
            admin.PasswordResetExpiry = null;
            admin.AccessFailedCount = 0;
            admin.LockoutEnd = null;

            _context.AuditTrails.Add(new AuditTrail
            {
                PerformedBy = "EmergencyResetAccess",
                Action = "Admin Password Reset",
                Timestamp = DateTime.Now,
                Details = $"Admin password reset for user '{admin.Username}' via emergency token at {DateTime.Now:yyyy-MM-dd HH:mm}"
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Password reset successfully!";
            return View();
        }

    }
