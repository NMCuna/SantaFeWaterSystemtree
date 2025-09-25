using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using QRCoder;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Models.ViewModels;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SantaFeWaterSystem.Controllers;

public class AccountController(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    IEmailSender emailSender,
    AuditLogService audit,
    IConfiguration configuration,
    LockoutService lockout,
    PasswordPolicyService passwordPolicyService 
) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly AuditLogService _audit = audit;
    private readonly IConfiguration _configuration = configuration;
    private readonly LockoutService _lockout = lockout;
    private readonly PasswordPolicyService _passwordPolicyService = passwordPolicyService;



    //////////////////////////////////
    //      ADMIN/STAFF LOGIN     ////
    //////////////////////////////////

    // ================== AdminLogin/ ADMIN AND STAFF CAN USE BUT BY PERMISSION ==================

    [HttpGet]
    public async Task<IActionResult> AdminLogin(string token)
    {
        // Get the token from the database
        var setting = await _context.AdminAccessSettings.FirstOrDefaultAsync();
        var requiredToken = setting?.LoginViewToken;

        // Validate the token
        if (string.IsNullOrEmpty(token) || token != requiredToken)
        {
            TempData["Error"] = "Unauthorized access.";
            return RedirectToAction("AccessDenied", "Account");
        }

        return View(new AdminLoginViewModel());
    }

    public IActionResult AccessDenied()
    {
        return View(); // Show an error message or redirect elsewhere
    }



    [HttpPost]
    public async Task<IActionResult> AdminLogin(AdminLoginViewModel model, [FromServices] LockoutService lockoutService)
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

        // Check if user already locked
        if (lockoutService.IsCurrentlyLocked(user))
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var unlockTimePH = TimeZoneInfo.ConvertTimeFromUtc(user.LockoutEnd!.Value, phTimeZone);

            // 🔹 Fetch lockout policy dynamically
            var policy = await _context.LockoutPolicies.FirstOrDefaultAsync();
            ViewBag.MaxAttempts = policy?.MaxFailedAccessAttempts ?? 5;
            ViewBag.LockoutMinutes = policy?.LockoutMinutes ?? 15;

            ViewBag.UnlockTime = unlockTimePH.ToString("yyyy-MM-ddTHH:mm:ss");
            ViewBag.UnlockTimeDisplay = unlockTimePH.ToString("f");
            ViewBag.Role = user.Role;

            await _audit.LogAsync("Locked Out",
                $"Login attempt while locked out - Role: {user.Role}, Username: {user.Username}",
                user.Username);

            return View("TemporaryLocked", user);
        }


        // Password validation
        bool passwordValid = false;

        if (!string.IsNullOrEmpty(user?.PasswordHash))
        {
            var hash = user.PasswordHash;

            if (hash.StartsWith("$2a$") || hash.StartsWith("$2b$") || hash.StartsWith("$2y$"))
            {
                // BCrypt hash
                try
                {
                    passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, hash);
                }
                catch
                {
                    passwordValid = false; // invalid salt / hash → treat as wrong password
                }
            }
            else
            {
                // Identity PBKDF2 hash
                var result = _passwordHasher.VerifyHashedPassword(user, hash, model.Password);
                passwordValid = result == PasswordVerificationResult.Success;
            }
        }


        if (!passwordValid)
        {
            var (isLocked, message) = await lockoutService.ApplyFailedAttemptAsync(user);

            ModelState.AddModelError("", message);

            if (isLocked)
                await _audit.LogAsync("Account Locked", $"User {user.Username} locked out.", user.Username);
            else
                await _audit.LogAsync("Failed Login", $"Invalid password attempt for user: {user.Username}", user.Username);

            return View(model);
        }

        // Valid password → reset failed count & lockout
        await lockoutService.ResetLockoutAsync(user);

        // Store user ID in session for 2FA
        HttpContext.Session.SetInt32("2FA_UserId", user.Id);
        HttpContext.Session.SetString("2FA_Expiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));

        await _audit.LogAsync("Login Success", $"User {user.Username} passed login and redirected to 2FA.", user.Username);

        if (!user.IsMfaEnabled)
            return RedirectToAction("Setup2FAAdmin", "Account");

        return RedirectToAction("Verify2FAAdmin", "Account");
    }











    /////////////////////////////////////
    /////ADMIN/STAFF/STAFF REGISTER ////
    ///////////////////////////////////

    // ================== RegisterAdmin/ FOR ADMIN ONLY ,CAN CREATE ADMIN AND STAFF ==================

    // ✅ GET: Register Admin
    [HttpGet]
    public async Task<IActionResult> RegisterAdmin()
    {
        ViewBag.PasswordPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();
        return View(new RegisterAdminViewModel());
    }

    // ✅ POST: Register Admin
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterAdmin(RegisterAdminViewModel model)
    {
        ViewBag.PasswordPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();

        if (!ModelState.IsValid)
            return View(model);

        var normalizedUsername = model.Username!.Trim().ToLower();
        bool usernameExists = await _context.Users
            .AnyAsync(u => u.Username != null && u.Username.ToLower() == normalizedUsername);

        if (usernameExists)
        {
            ModelState.AddModelError("Username", "Username already exists.");
            return View(model);
        }

        // 🔹 Validate password with policy
        var isValidPassword = await _passwordPolicyService.ValidatePasswordAsync(0, model.Password!);
        if (!isValidPassword)
        {
            ModelState.AddModelError("Password", "Password does not meet security policy requirements.");
            return View(model);
        }

        // 🔹 Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

        var user = new User
        {
            FullName = model.FullName!.Trim(),
            Username = model.Username!.Trim(),
            PasswordHash = hashedPassword,
            Role = model.Role!.Trim(),
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // 🔹 Save password history
        await _passwordPolicyService.SavePasswordHistoryAsync(user.Id, hashedPassword);

        // 🔹 Audit log
        var audit = new AuditTrail
        {
            Action = "Register Admin/Staff",
            PerformedBy = User.Identity?.Name ?? "Unknown",
            Timestamp = DateTime.UtcNow,
            Details = $"New user registered. Username: {user.Username}, Role: {user.Role}"
        };

        _context.AuditTrails.Add(audit);
        await _context.SaveChangesAsync();

        TempData["Message"] = "Admin/Staff registered successfully.";
        return RedirectToAction("ManageUsers", "Admin", new { roleFilter = "Admin" });
    }







    /////////////////////////////////////
    /////      USER  REGISTER       ////
    ///////////////////////////////////

    // ================== RegisterUser/ACTION FOR ADMIN,STAFF TO CREATE USER ==================

    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> RegisterUser()
    {
        ViewBag.PasswordPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();
        return View(new UserRegisterViewModel());
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterUser(UserRegisterViewModel model)
    {
        ViewBag.PasswordPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();

        if (!ModelState.IsValid)
            return View(model);

        // 🔹 Check if Account Number exists
        bool accountExists = await _context.Users.AnyAsync(u => u.AccountNumber == model.AccountNumber);
        if (accountExists)
        {
            ModelState.AddModelError("AccountNumber", "Account number already exists.");
            return View(model);
        }

        // 🔹 Check if Username exists
        bool usernameExists = await _context.Users.AnyAsync(u => u.Username == model.Username);
        if (usernameExists)
        {
            ModelState.AddModelError("Username", "Username already exists.");
            return View(model);
        }

        // 🔹 Validate password
        bool isValidPassword = await _passwordPolicyService.ValidatePasswordAsync(0, model.Password!);
        if (!isValidPassword)
        {
            ModelState.AddModelError("Password", "Password does not meet security policy requirements.");
            return View(model);
        }

        // 🔹 Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

        // 🔹 Create User
        var user = new User
        {
            AccountNumber = model.AccountNumber!.Trim(),
            Username = model.Username!.Trim(),
            Role = "User",
            PasswordHash = hashedPassword,
            AccessFailedCount = 0,
            IsMfaEnabled = false,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // 🔹 Save password history
        await _passwordPolicyService.SavePasswordHistoryAsync(user.Id, hashedPassword);

        // 🔹 Audit Log
        var performedBy = User.Identity?.Name ?? "Unknown";
        var audit = new AuditTrail
        {
            Action = "Register User",
            PerformedBy = performedBy,
            Timestamp = DateTime.UtcNow,
            Details = $"Registered new user '{user.Username}' (Account: {user.AccountNumber})."
        };
        _context.AuditTrails.Add(audit);
        await _context.SaveChangesAsync();

        TempData["Message"] = "User registered successfully!";
        return RedirectToAction("ManageUsers", "Admin", new { roleFilter = "User" });
    }





    ///////////////////////////
    /////Account/UserLogin////
    ///////////////////////////

    // ================== UserLogin ==================

    [HttpGet]
        public IActionResult UserLogin()
        {
            return View(new UserLoginViewModel());  // pass empty model to ensure no values //
        }


    // POST: /Account/UserLogin
    [HttpPost]
    public async Task<IActionResult> UserLogin(UserLoginViewModel model, [FromServices] LockoutService lockoutService)
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

        // ✅ Check if admin locked the account
        if (user.IsLocked)
        {
            await _audit.LogAsync("Login Blocked", "Login blocked due to admin-locked account.", user.AccountNumber);
            return View("AccountLocked", user);
        }

        // ✅ Check lockout from failed attempts
        if (lockoutService.IsCurrentlyLocked(user))
        {
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var unlockTimePH = TimeZoneInfo.ConvertTimeFromUtc(user.LockoutEnd!.Value, phTimeZone);

            // 🔹 Fetch lockout policy dynamically
            var policy = await _context.LockoutPolicies.FirstOrDefaultAsync();
            ViewBag.MaxAttempts = policy?.MaxFailedAccessAttempts ?? 5;
            ViewBag.LockoutMinutes = policy?.LockoutMinutes ?? 15;

            ViewBag.UnlockTime = unlockTimePH.ToString("yyyy-MM-ddTHH:mm:ss");
            ViewBag.UnlockTimeDisplay = unlockTimePH.ToString("f");
            ViewBag.Role = user.Role;

            await _audit.LogAsync("Locked Out",
                $"Login attempt blocked due to lockout. Try again at {unlockTimePH:f} (PH time).",
                user.AccountNumber);

            return View("TemporaryLocked", user);
        }

        // ✅ Validate password
        bool passwordValid = false;
        if (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$"))
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
        }
        else
        {
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            passwordValid = result == PasswordVerificationResult.Success;

            // ✅ Optional: rehash to BCrypt for new login
            if (passwordValid)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                await _context.SaveChangesAsync();
            }
        }

        if (!passwordValid)
        {
            var (isLocked, message) = await lockoutService.ApplyFailedAttemptAsync(user);
            ModelState.AddModelError("", message);

            await _audit.LogAsync(isLocked ? "Account Locked" : "Failed Login",
                $"Failed login for user {user.AccountNumber}. Attempts: {user.AccessFailedCount}. {message}",
                user.AccountNumber);

            return View(model);
        }

        // ✅ Success → reset lockout
        await lockoutService.ResetLockoutAsync(user);

        if (user.IsMfaEnabled)
        {
            HttpContext.Session.SetInt32("2FA_UserId", user.Id);
            await _audit.LogAsync("2FA Login Initiated", "User login passed password check. Redirected to 2FA.", user.AccountNumber);
            return RedirectToAction("Verify2FAUser");
        }

        // ✅ Sign in user
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.AccountNumber ?? user.Username ?? ""),
        new Claim(ClaimTypes.Role, user.Role),
        new Claim("UserId", user.Id.ToString())
    };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // ✅ Privacy check
        var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.UserId == user.Id);
        if (consumer != null)
        {
            var latestPolicy = await _context.PrivacyPolicies.OrderByDescending(p => p.Version).FirstOrDefaultAsync();
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







    /////////////////////////////////////
    /////ADMIN/STAFF/USER LOGOUT ////
    ///////////////////////////////////

    // ================== Logout/USE BY USER,ADMIN,STAFF ==================
    [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Get the currently logged-in user's name or ID for audit //
            var username = User.Identity?.Name ?? "Unknown";

            // Sign out and clear auth cookie //
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clear session
            HttpContext.Session.Clear();
        
            //  Log logout action via service //
            await _audit.LogAsync("Logout", "User logged out successfully.", username);

            // Redirect to homepage or login //
            return RedirectToAction("Index", "Home");
        }












    ///////////////////////////////////////////////
    ///// CONSUMER RESET PASS/ IN MANAGE USER /////
    ///////////////////////////////////////////////

    // ================== ResetPassword/ADMIN ACTION TO RESET USER PASS ==================
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public IActionResult ResetPassword(int id, int page = 1, string? roleFilter = null, string? searchTerm = null)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
            return NotFound();

        // Load password policy
        var passwordPolicy = _context.PasswordPolicies.FirstOrDefault();
        ViewBag.PasswordPolicy = passwordPolicy;

        // Keep navigation info
        ViewBag.Page = page;
        ViewBag.RoleFilter = roleFilter;
        ViewBag.SearchTerm = searchTerm;

        return View("ResetPassword", user);
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        int id, string NewPassword, string ConfirmPassword,
        int page = 1, string? roleFilter = null, string? searchTerm = null)
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ModelState.AddModelError(string.Empty, "Both password fields are required.");
        }
        else if (NewPassword != ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        // Always load password policy for redisplay
        var passwordPolicy = _context.PasswordPolicies.FirstOrDefault();
        ViewBag.PasswordPolicy = passwordPolicy;

        if (!ModelState.IsValid)
        {
            ViewBag.Page = page;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.SearchTerm = searchTerm;
            return View(user);
        }

        // ✅ Enforce password policy
        var isValidPassword = await _passwordPolicyService.ValidatePasswordAsync(user.Id, NewPassword);
        if (!isValidPassword)
        {
            ModelState.AddModelError(string.Empty, "Password does not meet security policy requirements.");
            ViewBag.Page = page;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.SearchTerm = searchTerm;
            return View(user);
        }

        // ✅ Hash new password using BCrypt
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(NewPassword);
        user.PasswordHash = hashedPassword;

        // ✅ Save password history
        await _passwordPolicyService.SavePasswordHistoryAsync(user.Id, hashedPassword);

        await _context.SaveChangesAsync();

        // ✅ Audit log
        _context.AuditTrails.Add(new AuditTrail
        {
            Action = "Reset Password (Admin)",
            PerformedBy = User.Identity?.Name ?? "Unknown",
            Timestamp = DateTime.UtcNow,
            Details = $"Password for user '{GetUserIdentifier(user)}' was reset by an admin."
        });

        await _context.SaveChangesAsync();

        TempData["Message"] = $"Password for {GetUserIdentifier(user)} has been reset successfully.";

        return RedirectToAction("ManageUsers", "Admin", new
        {
            roleFilter = roleFilter ?? user.Role,
            page,
            searchTerm
        });
    }

    private string GetUserIdentifier(User user)
    {
        return user.Role == "User"
            ? user.AccountNumber ?? "Unknown Account Number"
            : user.Username ?? "Unknown Username";
    }
















    /////////////////////////////////////////////////
    //    CONSUMER FORGOT PASS/ IN USERLOGIN      //
    ////////////////////////////////////////////////

    // ================== ForgotPasswordUser/POST WILL SEND LINK TO GMAIL ==================
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

        // AuditTrail
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



    // ================== ForgotPasswordUserConfirmation ==================
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











    /////////////////////////////////////////////////////////   
    //    CONSUMER AFTER FORGOT PASS/ IN GMAIL LINK       //
    /////////////////////////////////////////////////////////

    // ================== Reset Password via Email Token ==================
    [HttpGet]
    public async Task<IActionResult> ResetPasswordUser(string token)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest("A token must be supplied for password reset.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PasswordResetToken == token && u.PasswordResetExpiry > DateTime.UtcNow);

        if (user == null)
        {
            return View("ResetPasswordUserInvalid"); // Show a user-friendly error
        }

        // ✅ Load password policy for display
        var passwordPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();
        ViewBag.PasswordPolicy = passwordPolicy;

        var model = new ResetPasswordUserViewModel { Token = token };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPasswordUser(ResetPasswordUserViewModel model)
    {
        // Always load password policy
        var passwordPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();
        ViewBag.PasswordPolicy = passwordPolicy;

        if (!ModelState.IsValid)
            return View(model);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PasswordResetToken == model.Token && u.PasswordResetExpiry > DateTime.UtcNow);

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid or expired password reset link.");
            return View(model);
        }

        // Check password policy
        var isValidPassword = await _passwordPolicyService.ValidatePasswordAsync(user.Id, model.NewPassword);
        if (!isValidPassword)
        {
            ModelState.AddModelError(string.Empty, "Password does not meet security policy requirements.");
            return View(model);
        }

        // Hash and save password using BCrypt
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.PasswordHash = hashedPassword;

        // Save password history
        await _passwordPolicyService.SavePasswordHistoryAsync(user.Id, hashedPassword);

        // Clear reset token
        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;

        await _context.SaveChangesAsync();

        //  Audit trail
        _context.AuditTrails.Add(new AuditTrail
        {
            Action = "Reset Password (User)",
            PerformedBy = user.Username ?? $"UserId:{user.Id}",
            Timestamp = DateTime.UtcNow,
            Details = "Password reset successfully via email token link."
        });

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ResetPasswordUserConfirmation));
    }


    // ================== ResetPasswordUserConfirmation ==================
    [HttpGet]
        public IActionResult ResetPasswordUserConfirmation()
        {
            return View();
        }


    // ================== LogAudit ==================
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







    //////////////////////////////////////////////////////////
    //            ACCOUNT / ADMIN/STAFF 2FA                 //
    //////////////////////////////////////////////////////////

    // ================== Setup2FAAdmin ==================
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

            //  Do NOT sign in the user here
            //  Keep session (2FA_UserId) active so Verify2FAAdmin can finish login

            TempData["Success"] = "2FA setup complete. Please verify your code to continue.";
            return RedirectToAction("Verify2FAAdmin", "Account");
        }


    // ================== Verify2FAAdmin ==================
    [HttpGet]
        public IActionResult Verify2FAAdmin()
        {
            // Get the user ID from session (set during login)
            var userId = HttpContext.Session.GetInt32("2FA_UserId");

            //  NEW: Get the 2FA expiry timestamp
            var expiryStr = HttpContext.Session.GetString("2FA_Expiry");

            //  NEW: Check if userId or expiry is missing or expired
            if (userId == null || string.IsNullOrEmpty(expiryStr) ||
                !DateTime.TryParse(expiryStr, out var expiryTime) || DateTime.UtcNow > expiryTime)
            {
                //  NEW: Cleanup session keys if expired
                HttpContext.Session.Remove("2FA_UserId");
                HttpContext.Session.Remove("2FA_Expiry");

                TempData["Error"] = "2FA session expired. Please login again.";
                return RedirectToAction("AdminLogin");
            }

            //  Render the 2FA verification page
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

            //  Build claims
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Username!),
        new Claim(ClaimTypes.Role, user.Role!),
        new Claim("UserId", user.Id.ToString())
    };

            // Role-based permissions
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
            "ManageCommunity","BackupManagement","GeneralSettings","ManageInquiries", "ViewAuditLogs",
            "ViewSmsLogs","ViewEmailLogs", "ManageLockoutPolicy", "ManagePasswordPolicy","ManageAccountPolicy",
            "ManageEmailSettings"
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

            // Sign in the user
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Clear 2FA session temp ID
            HttpContext.Session.Remove("2FA_UserId");

            // Audit the success (use your audit service)
            await _audit.LogAsync("2FA Verified", $"Admin/Staff {user.Username} completed 2FA and signed in.", user.Username);

            TempData["Success"] = "2FA verified. Welcome!";
            return RedirectToAction("Dashboard", "Admin");
        }


    // ================== Reset2FAAdmin ==================
    [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reset2FAAdmin()
        {
            // Use the same claim as in login: "UserId"
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return RedirectToAction("AdminLogin");

            var user = await _context.Users.FindAsync(userId);
            if (user == null || (user.Role != "Admin" && user.Role != "Staff"))
                return Unauthorized();

            //  Clear 2FA settings
            user.IsMfaEnabled = false;
            user.MfaSecret = null;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Log reset action
            await _audit.LogAsync("2FA Reset", $"Admin/Staff {user.Username} reset their 2FA settings.", user.Username);
            // Sign out for security
            await HttpContext.SignOutAsync();

            TempData["Success"] = "Two-Factor Authentication has been reset. Please log in again.";
            return RedirectToAction("AdminLogin", "Account");
        }






    //////////////////////////////////////////////////////////
    //                  ACCOUNT / USER  2FA                 //
    //////////////////////////////////////////////////////////

    // ================== Setup2FAUser ==================
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


    // ================== Verify2FAUser ==================
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

            // Sign in with claims
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.AccountNumber!),
        new Claim(ClaimTypes.Role, user.Role!),
        new Claim("UserId", user.Id.ToString())
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // Audit using central logging service
            string performedBy = $"{user.AccountNumber} ({user.Username})";
            await _audit.LogAsync("2FA Verified", "User completed 2FA and logged in successfully.", performedBy);

            TempData["Success"] = "2FA passed. Welcome!";
            return RedirectToAction("Dashboard", "User");
        }

    // ================== Reset2FAUser ==================
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

            // Audit log
            string performedBy = $"{user.AccountNumber} ({user.Username})";
            await _audit.LogAsync("2FA Reset", "User disabled 2FA on their account.", performedBy);

            TempData["Success"] = "2FA has been disabled.";
            return RedirectToAction("Dashboard", "User");
        }














    //////////////////////////////////////////////////////////
    ////      ACCOUNT / BACKUP RESET PASSADMIN  2FA       ////
    //////////////////////////////////////////////////////////

    // ================== MySecretEmergenceResetPasswordforAdmin (GET) ==================
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> MySecretEmergenceResetPasswordforAdmin(string token)
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(12, 0, 0);
        var end = new TimeSpan(17, 0, 0);

        var today = DateTime.Today.DayOfWeek.ToString();
        var expectedToken = await _context.AdminResetTokens
            .Where(t => t.Day == today)
            .Select(t => t.Token)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(token) || token != expectedToken)
        {
            TempData["Error"] = "⛔ Invalid or missing token. Please contact the developer.";
            return RedirectToAction("AccessDenied", "Account");
        }

        // Load admins
        var admins = await _context.Users
            .Where(u => u.Role == "Admin")
            .Select(u => new AdminDropdown { Id = u.Id, Username = u.Username })
            .ToListAsync();

        // Load password policy
        var passwordPolicy = await _context.PasswordPolicies.FirstOrDefaultAsync();

        var vm = new AdminResetPasswordViewModel
        {
            Token = token,
            AdminList = admins,
            PasswordPolicy = passwordPolicy,
            CurrentTime = DateTime.Now.ToString("hh:mm tt"),
            TimeWarning = (now < start || now > end) ? "⏰ This page is only available between 12:00 PM and 5:00 PM." : null
        };

        return View(vm);
    }


    // ================== MySecretEmergenceResetPasswordforAdmin (POST) ==================
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MySecretEmergenceResetPasswordforAdmin(AdminResetPasswordViewModel model)
    {
        var today = DateTime.Today.DayOfWeek.ToString();
        var expectedToken = await _context.AdminResetTokens
            .Where(t => t.Day == today)
            .Select(t => t.Token)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(model.Token) || model.Token != expectedToken)
        {
            TempData["Error"] = "⛔ Token expired or invalid. Please request a new one.";
            return RedirectToAction("MySecretEmergenceResetPasswordforAdmin");
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            TempData["Error"] = "❌ Passwords do not match.";
            return RedirectToAction("MySecretEmergenceResetPasswordforAdmin", new { token = model.Token });
        }

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Id == model.AdminId && u.Role == "Admin");
        if (admin == null)
        {
            TempData["Error"] = "❌ Admin not found.";
            return RedirectToAction("MySecretEmergenceResetPasswordforAdmin", new { token = model.Token });
        }

        // Validate password policy
        var isValidPassword = await _passwordPolicyService.ValidatePasswordAsync(admin.Id, model.NewPassword);
        if (!isValidPassword)
        {
            TempData["Error"] = "❌ Password does not meet security policy requirements.";
            return RedirectToAction("MySecretEmergenceResetPasswordforAdmin", new { token = model.Token, AdminId = model.AdminId });
        }

        // Hash and save new password
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        admin.PasswordResetToken = null;
        admin.PasswordResetExpiry = null;
        admin.AccessFailedCount = 0;
        admin.LockoutEnd = null;

        // Save password history
        await _passwordPolicyService.SavePasswordHistoryAsync(admin.Id, admin.PasswordHash);

        // Audit log
        _context.AuditTrails.Add(new AuditTrail
        {
            PerformedBy = "EmergencyResetAccess",
            Action = "Admin Password Reset",
            Timestamp = DateTime.Now,
            Details = $"Admin password reset for user '{admin.Username}' via emergency token at {DateTime.Now:yyyy-MM-dd HH:mm}"
        });

        await _context.SaveChangesAsync();

        TempData["Success"] = "✅ Password reset successfully!";
        return RedirectToAction("MySecretEmergenceResetPasswordforAdmin", new { token = model.Token, AdminId = model.AdminId });
    }



}
