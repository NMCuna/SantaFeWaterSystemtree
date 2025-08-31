// AuditTrailController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using System.Linq;
using System.Threading.Tasks;
using X.PagedList;
using X.PagedList.Extensions;
using SantaFeWaterSystem.ViewModels;


namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditTrailController : BaseController
    {      
        public AuditTrailController(ApplicationDbContext context, PermissionService permissionService, AuditLogService audit)
            : base(permissionService, context, audit)
        {
          
        }



        // ================== INDEX LIST OF THE ADITTRAIL ==================

        public IActionResult Index(string month, string actionType, string search, int page = 1)
        {
            try
            {
                int pageSize = 8;
                var query = _context.AuditTrails.AsQueryable();

                // Filter by month
                DateTime start, end;
                if (!string.IsNullOrEmpty(month) && DateTime.TryParse(month + "-01", out var selectedMonth))
                {
                    start = selectedMonth;
                    end = start.AddMonths(1);
                }
                else
                {
                    var now = DateTime.Now;
                    start = new DateTime(now.Year, now.Month, 1);
                    end = start.AddMonths(1);
                }
                query = query.Where(a => a.Timestamp >= start && a.Timestamp < end);

                // Filter by action type
                if (!string.IsNullOrEmpty(actionType))
                {
                    query = query.Where(a => a.Action != null && a.Action.Contains(actionType));
                }

                // Move to memory for search filter
                var logs = query
                    .OrderByDescending(a => a.Timestamp)
                    .ToList();

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    string term = search.ToLower();
                    logs = logs.Where(a =>
                        (a.Action != null && a.Action.ToLower().Contains(term)) ||
                        (a.PerformedBy != null && a.PerformedBy.ToLower().Contains(term)) ||
                        (a.Details != null && a.Details.ToLower().Contains(term))
                    ).ToList();
                }

                // Pagination
                var pagedLogs = logs.ToPagedList(page, pageSize);

                return View(pagedLogs);
            }
            catch (Exception)
            {
                // Log the error if needed: _logger.LogError(ex, "Error loading audit logs");
                TempData["Error"] = "An error occurred while loading the audit logs. Please try again.";
                return View(new List<AuditTrail>().ToPagedList(page, 8)); // return empty list
            }
        }



        // ================== DETAILS ==================

        public IActionResult Details(int id)
        {
            var log = _context.AuditTrails.FirstOrDefault(a => a.Id == id);
            if (log == null)
            {
                return NotFound();
            }
            return View(log);
        }




        // ================== ARCHIVE OLD LOGS ==================

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ArchiveOldAuditLogs()
        {
            // Get Philippine Standard Time
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var nowPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

            // Set cutoff to 12 hours ago PH time
            var cutoffPH = nowPH.AddHours(-12);

            // Convert cutoff to UTC since log timestamps are likely stored in UTC
            var cutoffUTC = TimeZoneInfo.ConvertTimeToUtc(cutoffPH, phTimeZone);

            // Get logs older than 12 hours in PH time
            var oldLogs = await _context.AuditTrails
                .Where(log => log.Timestamp < cutoffUTC)
                .ToListAsync();

            if (!oldLogs.Any())
            {
                TempData["Info"] = "No logs older than 12 hours (PH time) to archive.";
                return RedirectToAction("Index");
            }

            // Archive the logs
            var archiveLogs = oldLogs.Select(log => new AuditTrailArchive
            {
                Action = log.Action,
                PerformedBy = log.PerformedBy,
                Timestamp = log.Timestamp,
                Details = log.Details
            }).ToList();

            _context.AuditTrailArchives.AddRange(archiveLogs);
            _context.AuditTrails.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{archiveLogs.Count} logs archived successfully.";
            return RedirectToAction("Index");
        }



        // ================== ARCHIVE LIST ==================

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AuditArchive(string? month, string? actionType, string? search, int page = 1)
        {
            int pageSize = 8;

            var query = _context.AuditTrailArchives.AsQueryable();

            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var nowPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

            DateTime start = !string.IsNullOrEmpty(month) && DateTime.TryParse(month + "-01", out var monthDate)
                ? new DateTime(monthDate.Year, monthDate.Month, 1)
                : new DateTime(nowPH.Year, nowPH.Month, 1);
            DateTime end = start.AddMonths(1);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(start, phTimeZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(end, phTimeZone);

            query = query.Where(a => a.Timestamp >= startUtc && a.Timestamp < endUtc);

            if (!string.IsNullOrEmpty(actionType))
            {
                query = query.Where(a => a.Action != null && a.Action.Contains(actionType));
            }

            // Move to memory so we can use .ToString()
            var logs = await query.ToListAsync();

            if (!string.IsNullOrEmpty(search))
            {
                string term = search.ToLower();
                logs = logs.Where(a =>
                    (a.Action != null && a.Action.ToLower().Contains(term)) ||
                    (a.PerformedBy != null && a.PerformedBy.ToLower().Contains(term)) ||
                    (a.Details != null && a.Details.ToLower().Contains(term)) ||
                    a.Timestamp.ToString("MMM dd, yyyy hh:mm tt").ToLower().Contains(term)
                ).ToList();
            }

            logs = logs.OrderByDescending(a => a.Timestamp).ToList();

            // Use non-async Create for in-memory list
            var pagedLogs = PaginatedList<AuditTrailArchive>.Create(logs, page, pageSize);
            return View(pagedLogs);
        }



        // ================== ARCHIVE DETAILS ==================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ArchiveDetails(int id)
        {
            var archiveLog = await _context.AuditTrailArchives.FindAsync(id);
            if (archiveLog == null)
                return NotFound();

            return View(archiveLog); 
        }
    }
}
