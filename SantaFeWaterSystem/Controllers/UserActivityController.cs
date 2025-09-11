using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System.Security.Claims;
using X.PagedList;
using X.PagedList.Extensions;
using SantaFeWaterSystem.Filters;



namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "User")]
    [RequirePrivacyAgreement]
    public class UserActivityController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;





        ///////////////////////////////////////
        //      TOP BAR SUPPORT /USER       //
        //////////////////////////////////////





        // ================== INDEX (SHOW THE LIST OF NEW CREATED ACTIVITY) ==================

        // GET: /UserActivity
        public IActionResult Index(string? actionType, string? month, int page = 1)
        {
            // Get current user's AccountNumber (stored in ClaimTypes.Name)
            var accountNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(accountNumber))
                return Unauthorized();

            //  Query logs by AccountNumber
            var query = _context.AuditTrails
                .Where(a => a.PerformedBy == accountNumber);

            // Filter by action type
            if (!string.IsNullOrEmpty(actionType))
            {
                query = query.Where(a => a.Action.Contains(actionType));
            }

            // Filter by month
            if (!string.IsNullOrEmpty(month) && DateTime.TryParse($"{month}-01", out var selectedMonth))
            {
                var nextMonth = selectedMonth.AddMonths(1);
                query = query.Where(a => a.Timestamp >= selectedMonth && a.Timestamp < nextMonth);
            }

            // Paginate
            var logs = query
                .OrderByDescending(a => a.Timestamp)
                .ToPagedList(page, 5);

            // Pass filters to view
            ViewBag.ActionType = actionType;
            ViewBag.Month = month;

            return View(logs);
        }




        // ================== ARCHIVE OWN LOGS (MOVE TO ARCHIVE TABLE) ==================

        // POST: /UserActivity/ArchiveOwnLogs
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ArchiveOwnLogs()
        {
            var accountNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(accountNumber))
                return Unauthorized();

            // Use local or UTC depending on your design
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            var nowPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
            var cutoff = nowPH.AddHours(-1);


            var oldLogs = await _context.AuditTrails
                .Where(log => log.PerformedBy == accountNumber && log.Timestamp <= cutoff)
                .ToListAsync();

            if (oldLogs.Any())
            {
                var archivedLogs = oldLogs.Select(log => new AuditTrailArchive
                {
                    Action = log.Action,
                    PerformedBy = log.PerformedBy,
                    Timestamp = log.Timestamp,
                    Details = log.Details
                }).ToList();

                _context.AuditTrailArchives.AddRange(archivedLogs);
                _context.AuditTrails.RemoveRange(oldLogs);

                await _context.SaveChangesAsync();

                TempData["Message"] = $"{archivedLogs.Count} of your logs were archived.";
            }
            else
            {
                TempData["Message"] = "No logs older than 1 hour to archive.";
            }

            return RedirectToAction("Index");
        }





        // ================== VIEW ARCHIVED LOGS ==================

        // GET: /UserActivity/Archive
        [Authorize(Roles = "User")]
        [HttpGet]
        public IActionResult Archive(string? actionType, string? dateRange, int page = 1)
        {
            var accountNumber = User.Identity?.Name;
            if (string.IsNullOrEmpty(accountNumber))
                return Unauthorized();

            var query = _context.AuditTrailArchives
                .Where(a => a.PerformedBy == accountNumber);

            // Filter by actionType
            if (!string.IsNullOrEmpty(actionType))
                query = query.Where(a => a.Action.Contains(actionType));

            // Filter by date range
            if (!string.IsNullOrEmpty(dateRange))
            {
                var dates = dateRange.Split(" to ");
                if (dates.Length == 2 &&
                    DateTime.TryParse(dates[0], out var startDate) &&
                    DateTime.TryParse(dates[1], out var endDate))
                {
                    // Include the whole end date (e.g., 2025-07-14 23:59:59)
                    endDate = endDate.AddDays(1);
                    query = query.Where(a => a.Timestamp >= startDate && a.Timestamp < endDate);
                }
            }

            var logs = query.OrderByDescending(a => a.Timestamp).ToPagedList(page, 5);

            // Maintain filters for the view
            ViewBag.ActionType = actionType;
            ViewBag.DateRange = dateRange;

            return View(logs);
        }




        // ================== BULK DELETE ARCHIVED LOGS ==================

        // POST: /UserActivity/BulkArchiveDelete
        [HttpPost]
        public async Task<IActionResult> BulkArchiveDelete(List<int> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Message"] = "No logs selected.";
                return RedirectToAction("Archive");
            }

            var logsToDelete = _context.AuditTrailArchives
                .Where(log => selectedIds.Contains(log.Id));

            _context.AuditTrailArchives.RemoveRange(logsToDelete);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Selected logs deleted successfully.";
            return RedirectToAction("Archive");
        }




        // ================== DELETE SINGLE ARCHIVED LOG ==================

        // POST: /UserActivity/DeleteArchive/5
        [HttpPost]
        public IActionResult DeleteArchive(int id)
        {
            var log = _context.AuditTrailArchives.Find(id);
            if (log != null)
            {
                _context.AuditTrailArchives.Remove(log);
                _context.SaveChanges();
            }
            return RedirectToAction("Archive");
        }
    }
}
