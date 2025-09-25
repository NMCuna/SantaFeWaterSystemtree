using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Models.ViewModels;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.ViewModels;
using System.Security.Claims;
using PermissionCheckbox = SantaFeWaterSystem.ViewModels.PermissionCheckbox;

namespace SantaFeWaterSystem.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class AdminController(PermissionService permissionService, ApplicationDbContext context, AuditLogService audit)
    : BaseController(permissionService, context, audit)
{



    [Authorize(Roles = "Admin,Staff")]
    private async Task<List<string>> GetUserPermissionsAsync()
        {
            // Get current logged in user ID (assuming user ID is stored in claims)
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return new List<string>();

            // Load user from DB including role and permissions
            var user = await _context.Users
                .Include(u => u.StaffPermissions)
                    .ThenInclude(sp => sp.Permission)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return new List<string>();

            // If user is Admin, get all permissions (full access)
            if (user.Role == "Admin")
            {
                return await _context.Permissions.Select(p => p.Name).ToListAsync();
            }

            // If user is Staff, get only assigned permissions
            if (user.Role == "Staff")
            {
                return user.StaffPermissions.Select(sp => sp.Permission.Name).ToList();
            }

            // For others, no permissions
            return new List<string>();


        }


    /////////////////////////////
    //  DASHBOARD CONTROLLER  //
    ////////////////////////////

    // ================== Dashboard ==================

    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalConsumers = await _context.Consumers.CountAsync();
            ViewBag.TotalBillings = await _context.Billings.CountAsync();
            ViewBag.TotalPayments = await _context.Payments.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;
            ViewBag.UnverifiedPayments = await _context.Payments.CountAsync(p => !p.IsVerified);

        //  Calculate consumers with 2 or more overdue unpaid bills
        var today = DateTime.Today;

        var pendingDisconnections = await _context.Billings
            .Where(b => !b.IsPaid && b.DueDate < today)
            .GroupBy(b => b.ConsumerId)
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .ToListAsync();

        ViewBag.PendingDisconnections = pendingDisconnections.Count;


        var currentYear = DateTime.UtcNow.Year;
            var monthlyPayments = await _context.Payments
                .Where(p => p.PaymentDate.Year == currentYear)
                .GroupBy(p => p.PaymentDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Total = g.Sum(p => p.AmountPaid)
                })
                .ToListAsync();

            var chartData = Enumerable.Range(1, 12).Select(m => new
            {
                Month = new DateTime(currentYear, m, 1).ToString("MMM"),
                Total = monthlyPayments.FirstOrDefault(mp => mp.Month == m)?.Total ?? 0m
            }).ToList();

            ViewBag.MonthlyPaymentChart = chartData;

            ViewBag.UserPermissions = await GetUserPermissionsAsync();

            //  Fix NullReferenceException on user ID
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return RedirectToAction("AdminLogin", "Account");
            }

            var user = await _context.Users.FindAsync(userId);
            ViewBag.IsMfaEnabled = user?.IsMfaEnabled ?? false;

            return View();
        }









    ///////////////////////////
    //     USER MANAGEMENT   //
    ///////////////////////////

    // ================== ManageUsers ==================

    // GET: Admin/UserList
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> ManageUsers(string? searchTerm, string? roleFilter, int page = 1, int pageSize = 5)
    {
        var model = new ManageUsersViewModel
        {
            SearchTerm = searchTerm ?? "",
            SelectedRole = roleFilter ?? "",
            CurrentPage = page
        };

        // Queries before search (for full summary counts)
        var allAdmins = _context.Users.Where(u => u.Role == "Admin");
        var allStaffs = _context.Users.Where(u => u.Role == "Staff");
        var allUsers = _context.Users.Where(u => u.Role == "User");

        model.TotalAdmins = await allAdmins.CountAsync();
        model.TotalStaffs = await allStaffs.CountAsync();
        model.TotalUsers = await allUsers.CountAsync();

        // Clone base queries
        var adminQuery = allAdmins;
        var staffQuery = allStaffs;
        var userQuery = allUsers;

        // Filter only the query for the current tab
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string lowerSearch = searchTerm.ToLower();
            switch (roleFilter)
            {
                case "Admin":
                    adminQuery = adminQuery.Where(u => !string.IsNullOrEmpty(u.Username) && u.Username.ToLower().Contains(lowerSearch));
                    break;
                case "Staff":
                    staffQuery = staffQuery.Where(u => !string.IsNullOrEmpty(u.Username) && u.Username.ToLower().Contains(lowerSearch));
                    break;
                case "User":
                    userQuery = userQuery.Where(u =>
                        (!string.IsNullOrEmpty(u.AccountNumber) && u.AccountNumber.ToLower().Contains(lowerSearch)) ||
                        (!string.IsNullOrEmpty(u.Username) && u.Username.ToLower().Contains(lowerSearch)));
                    break;
            }
        }

        // Assign filtered counts for table headers
        model.AdminCount = await adminQuery.CountAsync();
        model.StaffCount = await staffQuery.CountAsync();
        model.UserCount = await userQuery.CountAsync();

        // Load paginated data only for selected tab
        switch (roleFilter)
        {
            case "Admin":
                model.Admins = await adminQuery.OrderBy(u => u.Username)
                                               .Skip((page - 1) * pageSize)
                                               .Take(pageSize).ToListAsync();
                model.TotalPages = (int)Math.Ceiling(model.AdminCount / (double)pageSize);
                model.Staffs = new();
                model.Users = new();
                break;
            case "Staff":
                model.Staffs = await staffQuery.OrderBy(u => u.Username)
                                               .Skip((page - 1) * pageSize)
                                               .Take(pageSize).ToListAsync();
                model.TotalPages = (int)Math.Ceiling(model.StaffCount / (double)pageSize);
                model.Admins = new();
                model.Users = new();
                break;
            case "User":
            default:
                model.Users = await userQuery.OrderBy(u => u.Username)
                                             .Skip((page - 1) * pageSize)
                                             .Take(pageSize).ToListAsync();
                model.TotalPages = (int)Math.Ceiling(model.UserCount / (double)pageSize);
                model.Admins = new();
                model.Staffs = new();
                model.SelectedRole = "User"; // default to User tab
                break;
        }

        // Set search term only for the current tab
        ViewBag.AdminSearchTerm = roleFilter == "Admin" ? searchTerm : "";
        ViewBag.StaffSearchTerm = roleFilter == "Staff" ? searchTerm : "";
        ViewBag.UserSearchTerm = roleFilter == "User" ? searchTerm : "";

        return View(model);
    }



    // ================== EditConsumerUser ==================

    // GET: Admin/EditConsumerUser/5
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> EditConsumerUser(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null || user.Role != "User")
            return NotFound();

        var model = new EditConsumerUserViewModel
        {
            Id = user.Id,
            Username = user.Username,
            AccountNumber = user.AccountNumber,
            IsMfaEnabled = user.IsMfaEnabled,
            Role = user.Role,

            // Keep list state
            RoleFilter = roleFilter,
            SearchTerm = searchTerm,
            CurrentPage = page
        };

        return View("EditConsumerUser", model);
    }

    // POST: Admin/EditConsumerUser/5
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditConsumerUser(EditConsumerUserViewModel model)
    {
        if (!ModelState.IsValid)
            return View("EditConsumerUser", model);

        var user = await _context.Users.FindAsync(model.Id);
        if (user == null || user.Role != "User")
            return NotFound();

        // Duplicate check
        bool accountChanged = user.AccountNumber?.Trim() != model.AccountNumber?.Trim();
        if (accountChanged)
        {
            var duplicate = await _context.Users
                .Where(u => u.Id != model.Id && u.AccountNumber == model.AccountNumber)
                .FirstOrDefaultAsync();

            if (duplicate != null)
            {
                ModelState.AddModelError("AccountNumber", "This account number is already in use.");
                return View("EditConsumerUser", model);
            }
        }

        // Save old values for audit
        string oldAccountNumber = user.AccountNumber ?? "";
        string oldUsername = user.Username ?? "";
        bool oldMfa = user.IsMfaEnabled;

        // Update fields
        user.AccountNumber = model.AccountNumber?.Trim() ?? string.Empty;
        user.Username = model.Username?.Trim() ?? string.Empty;
        user.IsMfaEnabled = model.IsMfaEnabled;

        var auditDetails = new List<string> { $"User ID: {user.Id}" };
        if (oldAccountNumber != user.AccountNumber)
            auditDetails.Add($"Account Number: {oldAccountNumber} → {user.AccountNumber}");
        if (oldUsername != user.Username)
            auditDetails.Add($"Username: {oldUsername} → {user.Username}");
        if (oldMfa != user.IsMfaEnabled)
            auditDetails.Add($"MFA Enabled: {oldMfa} → {user.IsMfaEnabled}");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            if (auditDetails.Count > 1)
            {
                _context.AuditTrails.Add(new AuditTrail
                {
                    PerformedBy = (User?.Identity?.IsAuthenticated == true ? User?.Identity?.Name : null) ?? "System",
                    Action = "Edited Consumer User",
                    Timestamp = DateTime.Now,
                    Details = string.Join(", ", auditDetails)
                });

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Consumer user updated successfully.";

            // Keep filters/search/page
            return RedirectToAction("ManageUsers", new
            {
                roleFilter = model.RoleFilter,
                searchTerm = model.SearchTerm,
                page = model.CurrentPage
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError("", "Database error while updating. Please try again later.");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError("", "An unexpected error occurred. Please contact support.");
        }

        return View("EditConsumerUser", model);
    }




    // ================== EditAdmin/Staff User ==================

    // GET: Admin/EditAdminUser/5
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> EditAdminUser(int id, string? roleFilter = "", string? searchTerm = "", int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null || (user.Role?.Trim() != "Admin" && user.Role?.Trim() != "Staff"))
            return NotFound();

        var model = new EditAdminUserViewModel
        {
            Id = user.Id,
            Username = user.Username ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            IsMfaEnabled = user.IsMfaEnabled,
            Role = user.Role ?? string.Empty,
            RoleFilter = roleFilter,
            SearchTerm = searchTerm,
            CurrentPage = page
        };

        return View("EditAdminUser", model);
    }

    // POST: Admin/EditAdminUser
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAdminUser(EditAdminUserViewModel model)
    {
        if (!ModelState.IsValid)
            return View("EditAdminUser", model);

        var user = await _context.Users.FindAsync(model.Id);
        if (user == null || (user.Role?.Trim() != "Admin" && user.Role?.Trim() != "Staff"))
            return NotFound();

        // Store old values for audit
        string oldUsername = user.Username ?? "";
        string oldFullName = user.FullName ?? "";
        bool oldMfa = user.IsMfaEnabled;

        // Update user
        user.Username = model.Username?.Trim() ?? string.Empty;
        user.FullName = model.FullName?.Trim() ?? string.Empty;
        user.IsMfaEnabled = model.IsMfaEnabled;

        var auditDetails = new List<string> { $"User ID: {user.Id}" };
        if (oldUsername != user.Username)
            auditDetails.Add($"Username: {oldUsername} → {user.Username}");
        if (oldFullName != user.FullName)
            auditDetails.Add($"Full Name: {oldFullName} → {user.FullName}");
        if (oldMfa != user.IsMfaEnabled)
            auditDetails.Add($"MFA Enabled: {oldMfa} → {user.IsMfaEnabled}");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            if (auditDetails.Count > 1)
            {
                _context.AuditTrails.Add(new AuditTrail
                {
                    PerformedBy = User?.Identity?.Name ?? "System",
                    Action = "Edited Admin/Staff User",
                    Timestamp = DateTime.Now,
                    Details = string.Join(", ", auditDetails)
                });
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Admin/Staff user updated successfully.";

            // Redirect back to ManageUsers with same filters/page
            return RedirectToAction("ManageUsers", new
            {
                roleFilter = model.RoleFilter,
                searchTerm = model.SearchTerm,
                page = model.CurrentPage
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError("", "An error occurred while updating. Please try again.");
            return View("EditAdminUser", model);
        }
    }


    // ================== DeleteUser ==================


    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> DeleteUser(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        ViewBag.RoleFilter = roleFilter;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.Page = page;

        return View(user);
    }


    [Authorize(Roles = "Admin,Staff")]
    [HttpPost, ActionName("DeleteUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserConfirmed(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        //  Add audit trail
        var audit = new AuditTrail
        {
            PerformedBy = User.Identity?.Name ?? "System",
            Action = "Deleted User",
            Timestamp = DateTime.Now,
            Details = $"Deleted user ID: {user.Id}, Username: {user.Username}"
        };

        _context.Users.Remove(user);
        _context.AuditTrails.Add(audit);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "User deleted successfully.";

        return RedirectToAction("ManageUsers", new { roleFilter, searchTerm, page });
    }




    // ================== LockUser  ==================

    // Show Lock confirmation view
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> LockUser(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null || user.Role != "User")
            return NotFound();

        // Pass filters to ViewData (so form can include them)
        ViewData["RoleFilter"] = roleFilter;
        ViewData["SearchTerm"] = searchTerm;
        ViewData["Page"] = page;

        return View(user);
    }

    // POST Lock
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost, ActionName("LockUser")]
    public async Task<IActionResult> LockUserConfirmed(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null && user.Role == "User")
        {
            user.IsLocked = true;
            await _context.SaveChangesAsync();

            var performedBy = User?.Identity?.Name ?? "Unknown";
            await _audit.LogAsync("User Locked", $"User account {user.AccountNumber} was locked.", performedBy);
        }

        // Redirect back WITH pagination and filters
        return RedirectToAction("ManageUsers", new
        {
            roleFilter,
            searchTerm,
            page
        });
    }



    // ================== UnlockUser  ==================

    // GET UnlockUser
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> UnlockUser(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null || user.Role != "User")
            return NotFound();

        // Pass filters to ViewData for the form
        ViewData["RoleFilter"] = roleFilter;
        ViewData["SearchTerm"] = searchTerm;
        ViewData["Page"] = page;

        return View(user);
    }

    // POST UnlockUser
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    public async Task<IActionResult> UnlockUserConfirmed(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null && user.Role == "User")
        {
            user.IsLocked = false;
            await _context.SaveChangesAsync();

            var performedBy = User.Identity?.Name ?? "Unknown";
            await _audit.LogAsync("User Unlocked", $"User account {user.AccountNumber} was unlocked.", performedBy);
        }

        // Redirect back with filters and page
        return RedirectToAction("ManageUsers", new
        {
            roleFilter,
            searchTerm,
            page
        });
    }




    // ================== Reset2FA   ==================

    //  Admin reset 2FA for another Admin
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset2FAAdmin(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var targetUser = await _context.Users.FindAsync(id);
        if (targetUser == null || targetUser.Role != "Admin")
            return NotFound();

        targetUser.IsMfaEnabled = false;
        targetUser.MfaSecret = null;
        _context.Users.Update(targetUser);
        await _context.SaveChangesAsync();

        string performedBy = User.Identity?.Name ?? "Unknown";
        await _audit.LogAsync("2FA Reset",
            $"Admin {performedBy} reset 2FA for Admin '{targetUser.Username}'.", performedBy);

        TempData["Message"] = $"2FA for Admin '{targetUser.Username}' has been reset.";

        // Redirect back with filters and pagination preserved
        return RedirectToAction("ManageUsers", new
        {
            roleFilter,
            searchTerm,
            page
        });
    }



    // ================== Reset2FA for Staff ==================

    //  Admin reset 2FA for a Staff
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset2FAStaff(
     int id,
     string roleFilter,
     string searchTerm,
     int page = 1)
    {
        var targetUser = await _context.Users.FindAsync(id);
        if (targetUser == null || targetUser.Role != "Staff")
            return NotFound();

        targetUser.IsMfaEnabled = false;
        targetUser.MfaSecret = null;
        _context.Users.Update(targetUser);
        await _context.SaveChangesAsync();

        string performedBy = User.Identity?.Name ?? "Unknown";
        await _audit.LogAsync("2FA Reset", $"Admin {performedBy} reset 2FA for Staff '{targetUser.Username}'.", performedBy);

        TempData["Message"] = $"2FA for Staff '{targetUser.Username}' has been reset.";

        // Preserve filters & pagination
        return RedirectToAction("ManageUsers", new
        {
            roleFilter,
            searchTerm,
            page
        });
    }



    // ================== Reset2FA for User ==================

    //  Admin reset 2FA for a User
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset2FAUser(int id, string roleFilter, string searchTerm, int page = 1)
    {
        var targetUser = await _context.Users.FindAsync(id);
        if (targetUser == null || targetUser.Role != "User")
            return NotFound();

        targetUser.IsMfaEnabled = false;
        targetUser.MfaSecret = null;
        _context.Users.Update(targetUser);
        await _context.SaveChangesAsync();

        string performedBy = User.Identity?.Name ?? "Unknown";
        await _audit.LogAsync("2FA Reset", $"Admin/Staff {performedBy} reset 2FA for User '{targetUser.Username}'.", performedBy);

        TempData["Message"] = $"2FA for User '{targetUser.Username}' has been reset.";

        // Pass filters and pagination back
        return RedirectToAction("ManageUsers", new
        {
            roleFilter,
            searchTerm,
            page

        });
    }












    /////////////////////////////
    //  CONSUMER CONTROLLER   //
    ////////////////////////////

    // ================== Consumers ==================

    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> Consumers(string searchTerm, string addressFilter, int page = 1, int pageSize = 6)
        {
        searchTerm = searchTerm?.Trim() ?? string.Empty;
        addressFilter = addressFilter?.Trim() ?? string.Empty;

        var consumersQuery = _context.Consumers
                .Include(c => c.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var normalizedTerm = searchTerm.ToLower();
                consumersQuery = consumersQuery.Where(c =>
                    c.FirstName.ToLower().Contains(normalizedTerm) ||
                    c.LastName.ToLower().Contains(normalizedTerm) ||
                    c.Email.ToLower().Contains(normalizedTerm));
            }

            if (!string.IsNullOrEmpty(addressFilter))
            {
                consumersQuery = consumersQuery.Where(c => c.HomeAddress.Contains(addressFilter));
            }

            int totalConsumers = await consumersQuery.CountAsync();
            var consumers = await consumersQuery
                .OrderBy(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewData["searchTerm"] = searchTerm;
            ViewData["addressFilter"] = addressFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalConsumers / pageSize);

            return View(consumers);
        }


    // ================== CreateConsumer ==================

    // GET: CreateConsumer
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public IActionResult CreateConsumer()
    {
        var linkedUserIds = _context.Consumers
            .Where(c => c.UserId != null)
            .Select(c => c.UserId)
            .ToList();

        var availableUsers = _context.Users
            .Where(u => u.Role == "User" && !linkedUserIds.Contains(u.Id))
            .ToList();

        var model = new ConsumerViewModel
        {
            AccountTypes = Enum.GetValues(typeof(ConsumerType))
                .Cast<ConsumerType>()
                .Select(a => new SelectListItem
                {
                    Value = a.ToString(),
                    Text = a.ToString()
                }),
            AvailableUsers = availableUsers.Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Username
            })
        };

        return View(model);
    }


    // POST: CreateConsumer
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateConsumer(ConsumerViewModel model)
    {
        if (ModelState.IsValid)
        {
            var consumer = new Consumer
            {
                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,
                HomeAddress = model.HomeAddress,
                MeterAddress = model.MeterAddress,
                Email = model.Email,
                ContactNumber = model.ContactNumber,
                AccountType = model.AccountType,
                MeterNo = model.MeterNo,
                UserId = model.UserId,
                Status = "Active",
                IsDisconnected = false
            };

            try
            {
                _context.Consumers.Add(consumer);
                _context.SaveChanges();

                // Audit trail
                _context.AuditTrails.Add(new AuditTrail
                {
                    PerformedBy = User.Identity?.Name ?? "Unknown",
                    Action = "Created Consumer",
                    Timestamp = DateTime.Now,
                    Details = $"New Consumer Added: ID = {consumer.Id}, " +
                              $"Name = {consumer.LastName}, {consumer.FirstName} {consumer.MiddleName}, " +
                              $"Meter No = {consumer.MeterNo}, Email = {consumer.Email}, " +
                              $"Type = {consumer.AccountType}, Contact = {consumer.ContactNumber}, " +
                              $"Linked User ID = {consumer.UserId}"
                });

                _context.SaveChanges();

                TempData["SuccessMessage"] = "Consumer added successfully!";
                return RedirectToAction("Consumers", "Admin");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error saving consumer: " + ex.Message);
            }
        }

        // If validation fails, reload dropdowns
        var linkedUserIds = _context.Consumers
            .Where(c => c.UserId != null)
            .Select(c => c.UserId)
            .ToList();

        var availableUsers = _context.Users
            .Where(u => u.Role == "User" && !linkedUserIds.Contains(u.Id))
            .ToList();

        model.AccountTypes = Enum.GetValues(typeof(ConsumerType))
            .Cast<ConsumerType>()
            .Select(a => new SelectListItem
            {
                Value = a.ToString(),
                Text = a.ToString(),
                Selected = a == model.AccountType
            });

        model.AvailableUsers = availableUsers.Select(u => new SelectListItem
        {
            Value = u.Id.ToString(),
            Text = u.Username,
            Selected = u.Id == model.UserId
        });

        return View(model);
    }


    // ================== ConsumerDetails ==================

    // GET: ConsumerDetails
    [Authorize(Roles = "Admin,Staff")]
    public IActionResult ConsumerDetails(int id, int page = 1)
    {
        ViewBag.CurrentPage = page;
        ViewBag.Users = new SelectList(_context.Users, "Id", "AccountNumber");

            var consumer = _context.Consumers
                .Include(c => c.User)
                .FirstOrDefault(c => c.Id == id);

            if (consumer == null)
                return NotFound();

            //  Audit trail: log view of consumer details
            _context.AuditTrails.Add(new AuditTrail
            {
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Action = "Viewed Consumer Details",
                Timestamp = DateTime.Now,
                Details = $"Viewed details of Consumer ID: {consumer.Id}, Name: {consumer.LastName}, {consumer.FirstName} {consumer.MiddleName}, Meter No: {consumer.MeterNo}"
            });

            _context.SaveChanges(); // Save audit log

            return View(consumer);
        }






    // ================== GET: EditConsumer ==================
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    public async Task<IActionResult> EditConsumer(int id, int page = 1)
    {
        var consumer = await _context.Consumers.FindAsync(id);
        if (consumer == null)
            return NotFound();

        var vm = new EditConsumerViewModel
        {
            Id = consumer.Id,
            FirstName = consumer.FirstName,
            LastName = consumer.LastName,
            MiddleName = consumer.MiddleName,
            AccountType = consumer.AccountType,
            Email = consumer.Email,
            HomeAddress = consumer.HomeAddress,
            MeterAddress = consumer.MeterAddress,
            MeterNo = consumer.MeterNo,
            ContactNumber = consumer.ContactNumber,
            Status = consumer.Status,
            UserId = consumer.UserId
        };

        // Users already linked to other consumers (exclude current consumer)
        var linkedUserIds = _context.Consumers
            .Where(c => c.UserId != null && c.Id != consumer.Id)
            .Select(c => c.UserId);

        // Available users = all regular users except linked ones
        var availableUsers = await _context.Users
            .Where(u => !linkedUserIds.Contains(u.Id) && u.Role == "User") 
            .OrderBy(u => u.Username)
            .ToListAsync();

        // Build SelectList using Username
        var userSelectList = availableUsers
            .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.Username,
                Selected = u.Id == consumer.UserId
            })
            .ToList();

        ViewBag.Users = userSelectList;

        ViewBag.AccountTypes = new SelectList(Enum.GetValues(typeof(ConsumerType)), consumer.AccountType);
        ViewBag.CurrentPage = page;

        return View(vm);
    }



    // ================== POST: EditConsumer ==================
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditConsumer(EditConsumerViewModel model, int page = 1)
    {
        if (!ModelState.IsValid)
        {
            // Re-populate dropdowns
            var linkedUserIds = _context.Consumers
                .Where(c => c.UserId != null && c.Id != model.Id)
                .Select(c => c.UserId);

            var availableUsers = await _context.Users
                .Where(u => !linkedUserIds.Contains(u.Id))
                .ToListAsync();

            ViewBag.Users = new SelectList(availableUsers, "Id", "AccountNumber", model.UserId);
            ViewBag.AccountTypes = new SelectList(Enum.GetValues(typeof(ConsumerType)), model.AccountType);
            ViewBag.CurrentPage = page;
            return View(model);
        }

        // Safety check: Ensure selected UserId is not already linked to another consumer
        if (model.UserId != null)
        {
            bool userAlreadyLinked = await _context.Consumers
                .AnyAsync(c => c.UserId == model.UserId && c.Id != model.Id);

            if (userAlreadyLinked)
            {
                ModelState.AddModelError("UserId", "⚠ This user is already linked to another consumer.");

                // Re-populate dropdowns
                var linkedUserIds = _context.Consumers
                    .Where(c => c.UserId != null && c.Id != model.Id)
                    .Select(c => c.UserId);

                var availableUsers = await _context.Users
                    .Where(u => !linkedUserIds.Contains(u.Id))
                    .ToListAsync();

                ViewBag.Users = new SelectList(availableUsers, "Id", "AccountNumber", model.UserId);
                ViewBag.AccountTypes = new SelectList(Enum.GetValues(typeof(ConsumerType)), model.AccountType);
                ViewBag.CurrentPage = page;

                return View(model);
            }
        }

        var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.Id == model.Id);
        if (consumer == null) return NotFound();

        // Store old values for audit
        var oldValues = $"Name: {consumer.LastName}, {consumer.FirstName} {consumer.MiddleName}, " +
                        $"Email: {consumer.Email}, Address: {consumer.HomeAddress}, " +
                        $"MeterNo: {consumer.MeterNo}, Type: {consumer.AccountType}, UserId: {consumer.UserId}, Status: {consumer.Status}";

        // Update
        consumer.FirstName = model.FirstName;
        consumer.LastName = model.LastName;
        consumer.MiddleName = model.MiddleName;
        consumer.AccountType = model.AccountType;
        consumer.Email = model.Email;
        consumer.HomeAddress = model.HomeAddress;
        consumer.MeterAddress = model.MeterAddress;
        consumer.MeterNo = model.MeterNo;
        consumer.ContactNumber = model.ContactNumber;
        consumer.Status = model.Status;
        consumer.UserId = model.UserId;

        // Add audit trail
        _context.AuditTrails.Add(new AuditTrail
        {
            PerformedBy = User.Identity?.Name ?? "Unknown",
            Action = "Edited Consumer",
            Timestamp = DateTime.Now,
            Details = $"Updated Consumer ID: {consumer.Id}\nOld: {oldValues}\nNew: " +
                      $"Name: {consumer.LastName}, {consumer.FirstName} {consumer.MiddleName}, " +
                      $"Email: {consumer.Email}, Address: {consumer.HomeAddress}, " +
                      $"MeterNo: {consumer.MeterNo}, Type: {consumer.AccountType}, UserId: {consumer.UserId}, Status: {consumer.Status}"
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "✅ Consumer updated successfully!";
        return RedirectToAction("Consumers", new { page });
    }




    // ================== DeleteConsumer ==================

    // GET: Confirm Delete
    [Authorize(Roles = "Admin,Staff")]
    public IActionResult DeleteConsumer(int id, int page = 1)
    {
            var consumer = _context.Consumers
                .Include(c => c.User)
                .FirstOrDefault(c => c.Id == id);

            if (consumer == null)
                return NotFound();

        ViewBag.CurrentPage = page;

        return View(consumer);
        }

        // POST: Delete
        [HttpPost, ActionName("DeleteConsumer")]
        [ValidateAntiForgeryToken]
    public IActionResult DeleteConsumerConfirmed(int id, int page = 1)
    {
            var consumer = _context.Consumers
                .Include(c => c.User)
                .FirstOrDefault(c => c.Id == id);

            if (consumer == null)
                return NotFound();

            // Store deleted info for audit
            var deletedInfo = $"Deleted Consumer ID: {consumer.Id}, " +
                              $"Name: {consumer.LastName}, {consumer.FirstName} {consumer.MiddleName}, " +
                              $"Email: {consumer.Email}, Address: {consumer.HomeAddress}, " +
                              $"Meter No: {consumer.MeterNo}, Account Type: {consumer.AccountType}, " +
                              $"Linked User: {consumer.User?.Username}";

            _context.Consumers.Remove(consumer);

            _context.AuditTrails.Add(new AuditTrail
            {
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Action = "Deleted Consumer",
                Timestamp = DateTime.Now,
                Details = deletedInfo
            });

            _context.SaveChanges();
        return RedirectToAction(nameof(Consumers), new { page });
    }


    // ================== GetConsumerInfo (AJAX) ==================
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
        public async Task<IActionResult> GetConsumerInfo(int consumerId)
        {
            var consumer = await _context.Consumers.FindAsync(consumerId);
            if (consumer == null) return NotFound();

            var rate = await _context.Rates
                .Where(r => r.AccountType == consumer.AccountType && r.EffectiveDate <= DateTime.Today)
                .OrderByDescending(r => r.EffectiveDate)
                .FirstOrDefaultAsync();

            var previousReading = await _context.Billings
                .Where(b => b.ConsumerId == consumerId)
                .OrderByDescending(b => b.BillingDate)
                .Select(b => b.PresentReading)
                .FirstOrDefaultAsync();

        return Json(new
        {
            accountType = consumer.AccountType.ToString(),
            rate = rate?.RatePerCubicMeter.ToString("F2") ?? "0.00",
            penalty = rate?.PenaltyAmount.ToString("F2") ?? "0.00",
            previousReading
        });
    }






    //////////////////////////////////////
    //   EDIT PERMISSIONS CONTROLLER   //
    /////////////////////////////////////

    // ================== EditPermissions ==================

    [HttpGet]
    public IActionResult EditPermissions(int staffId, string roleFilter, string searchTerm, int page = 1)
    {
        var allPermissions = _context.Permissions.ToList();

        var userPermissions = _context.StaffPermissions
                               .Where(sp => sp.StaffId == staffId)
                               .Select(sp => sp.PermissionId)
                               .ToList();

        var model = new EditPermissionsViewModel
        {
            StaffId = staffId,
            Permissions = allPermissions.Select(p => new PermissionCheckbox
            {
                PermissionId = p.Id,
                Name = p.Name,
                IsAssigned = userPermissions.Contains(p.Id)
            }).ToList(),
            // Add these properties to ViewModel
            SelectedRole = roleFilter,
            SearchTerm = searchTerm,
            CurrentPage = page
        };

        return View(model);
    }

    [HttpPost]
    public IActionResult EditPermissions(EditPermissionsViewModel model)
    {
        // Remove existing permissions
        var existing = _context.StaffPermissions
            .Where(sp => sp.StaffId == model.StaffId);
        _context.StaffPermissions.RemoveRange(existing);

        // Get assigned permissions
        var assignedPermissions = model.Permissions
            .Where(p => p.IsAssigned)
            .Select(p => p.Name)
            .ToList();

        // Add newly assigned permissions
        foreach (var p in model.Permissions.Where(p => p.IsAssigned))
        {
            _context.StaffPermissions.Add(new StaffPermission
            {
                StaffId = model.StaffId,
                PermissionId = p.PermissionId
            });
        }

        _context.SaveChanges();

        // Get staff for audit
        var staff = _context.Users.FirstOrDefault(u => u.Id == model.StaffId);
        string staffInfo = staff != null
            ? $"{staff.FullName} ({staff.Username})"
            : $"Staff ID: {model.StaffId}";

        // Audit log
        _context.AuditTrails.Add(new AuditTrail
        {
            PerformedBy = User.Identity?.Name ?? "Unknown",
            Action = "Edited Staff Permissions",
            Timestamp = DateTime.Now,
            Details = $"Permissions updated for {staffInfo}. Assigned: " +
                      (assignedPermissions.Any() ? string.Join(", ", assignedPermissions) : "None")
        });

        _context.SaveChanges();

        // Redirect back WITH filters & pagination
        return RedirectToAction("ManageUsers", new
        {
            roleFilter = model.SelectedRole,
            searchTerm = model.SearchTerm,
            page = model.CurrentPage
        });
    }


}








