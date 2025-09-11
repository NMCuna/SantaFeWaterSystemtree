using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Helpers;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using X.PagedList;


namespace SantaFeWaterSystem.Controllers
{
    public class ReportsController(ApplicationDbContext context, IWebHostEnvironment env, PermissionService permissionService, AuditLogService audit) : BaseController(permissionService, context, audit)
    {
        private readonly IWebHostEnvironment _env = env;



        //================== REPORT VIEW LIST ==================

        // GET: Reports
        public async Task<IActionResult> Index(
     DateTime? startDate,
     DateTime? endDate,
     int? consumerId,
     string billingStatus,
     string selectedMonth,
     int paymentsPage = 1,
     int billingsPage = 1)
        {
            int pageSize = 5;

            var billingsQuery = _context.Billings.Include(b => b.Consumer).AsQueryable();
            var paymentsQuery = _context.Payments.Include(p => p.Consumer).AsQueryable();

            // If selectedMonth is specified and start/end dates are not, set startDate and endDate accordingly
            if (!string.IsNullOrWhiteSpace(selectedMonth) &&
                DateTime.TryParse($"{selectedMonth}-01", out var monthDate) &&
                !startDate.HasValue && !endDate.HasValue)
            {
                startDate = new DateTime(monthDate.Year, monthDate.Month, 1);
                endDate = startDate.Value.AddMonths(1).AddDays(-1);
            }

            // Filter billings by billing date range
            if (startDate.HasValue)
            {
                billingsQuery = billingsQuery.Where(b => b.BillingDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                billingsQuery = billingsQuery.Where(b => b.BillingDate <= endDate.Value);
            }

            // Filter by consumer on both billings and payments
            if (consumerId.HasValue)
            {
                billingsQuery = billingsQuery.Where(b => b.ConsumerId == consumerId.Value);
                paymentsQuery = paymentsQuery.Where(p => p.ConsumerId == consumerId.Value);
            }

            // Filter billings by billing status
            if (!string.IsNullOrWhiteSpace(billingStatus) && billingStatus != "All")
            {
                if (billingStatus == "Unpaid")
                {
                    billingsQuery = billingsQuery.Where(b => b.Status == "Pending" || b.Status == "Unpaid");
                }
                else
                {
                    billingsQuery = billingsQuery.Where(b => b.Status == billingStatus);
                }
            }

            // Execute queries to get filtered data
            var billingsList = await billingsQuery.ToListAsync();
            var paymentsList = await paymentsQuery.ToListAsync();

            // Get available billing months for dropdown filter
            var availableMonths = _context.Billings
                .AsNoTracking()
                .Select(b => b.BillingDate)
                .AsEnumerable()
                .Select(d => new DateTime(d.Year, d.Month, 1))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            // Calculate revenue grouped by billing month (not payment date)
            var paymentsWithBillingDate = from p in _context.Payments
                                          join b in _context.Billings
                                            on p.BillingId equals b.Id
                                          select new
                                          {
                                              p.AmountPaid,
                                              b.ConsumerId,
                                              BillingYear = b.BillingDate.Year,
                                              BillingMonth = b.BillingDate.Month
                                          };

            // Filter payments based on billing date range (using year/month integer comparison for EF Core translation)
            if (startDate.HasValue)
            {
                int startYear = startDate.Value.Year;
                int startMonthNum = startDate.Value.Month;

                paymentsWithBillingDate = paymentsWithBillingDate.Where(x =>
                    (x.BillingYear > startYear) ||
                    (x.BillingYear == startYear && x.BillingMonth >= startMonthNum));
            }

            if (endDate.HasValue)
            {
                int endYear = endDate.Value.Year;
                int endMonthNum = endDate.Value.Month;

                paymentsWithBillingDate = paymentsWithBillingDate.Where(x =>
                    (x.BillingYear < endYear) ||
                    (x.BillingYear == endYear && x.BillingMonth <= endMonthNum));
            }

            // Filter by consumer if specified
            if (consumerId.HasValue)
            {
                paymentsWithBillingDate = paymentsWithBillingDate.Where(x => x.ConsumerId == consumerId.Value);
            }

            // Group payments by billing year and month and calculate total revenue per group
            var revenueByBillingMonth = await paymentsWithBillingDate
                .GroupBy(x => new { x.BillingYear, x.BillingMonth })
                .Select(g => new RevenueByMonthViewModel
                {
                    Year = g.Key.BillingYear,
                    Month = g.Key.BillingMonth,
                    TotalRevenue = g.Sum(x => x.AmountPaid)
                })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToListAsync();

            // Prepare ViewModel
            var viewModel = new ReportsViewModel
            {
                PagedBillings = await billingsQuery
                    .OrderByDescending(b => b.BillingDate)
                    .ToPagedListAsync(billingsPage, pageSize),

                PagedPayments = await paymentsQuery
                    .OrderByDescending(p => p.PaymentDate)
                    .ToPagedListAsync(paymentsPage, pageSize),

                Billings = billingsList,
                Payments = paymentsList,
                Consumers = await _context.Consumers.ToListAsync(),

                FilterStartDate = startDate,
                FilterEndDate = endDate,
                SelectedConsumerId = consumerId,
                SelectedBillingStatus = billingStatus ?? "All",
                SelectedMonth = selectedMonth,
                AvailableMonths = availableMonths,

                TotalRevenue = revenueByBillingMonth.Sum(r => r.TotalRevenue),
                RevenueByBillingMonth = revenueByBillingMonth
            };

            return View(viewModel);
        }


        //================== EXPORT TO PDF ==================

        // GET: Reports/DownloadMonthlyReport?selectedMonth=2025-07
        [HttpGet]
        public async Task<IActionResult> DownloadMonthlyReport(string selectedMonth)
        {
            if (string.IsNullOrEmpty(selectedMonth) ||
                !DateTime.TryParse($"{selectedMonth}-01", out var monthDate))
            {
                return BadRequest("Invalid month format.");
            }

            var start = new DateTime(monthDate.Year, monthDate.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var billings = await _context.Billings
                .Include(b => b.Consumer)
                .Where(b => b.BillingDate >= start && b.BillingDate <= end)
                .ToListAsync();

            // Join payments to billings to get payments for the selected billing month
            var payments = await (from p in _context.Payments.Include(p => p.Consumer)
                                  join b in _context.Billings on p.BillingId equals b.Id
                                  where b.BillingDate >= start && b.BillingDate <= end
                                  select p).ToListAsync();

            decimal totalCubicMeterUsed = billings.Sum(b => b.CubicMeterUsed);

            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
            var pdf = ReportPdfService.GenerateReport(billings, payments, logoPath, totalCubicMeterUsed);

            var fileName = $"MonthlyReport_{start:yyyy_MM}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(DateTime? startDate, DateTime? endDate, int? consumerId, string billingStatus, string selectedMonth)
        {
            var billings = _context.Billings.Include(b => b.Consumer).AsQueryable();

            // If month selected, override start and end dates
            if (!string.IsNullOrEmpty(selectedMonth) &&
                DateTime.TryParse($"{selectedMonth}-01", out var monthDate))
            {
                startDate = new DateTime(monthDate.Year, monthDate.Month, 1);
                endDate = startDate.Value.AddMonths(1).AddDays(-1);
            }

            if (startDate.HasValue)
            {
                billings = billings.Where(b => b.BillingDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                billings = billings.Where(b => b.BillingDate <= endDate.Value);
            }

            if (consumerId.HasValue)
            {
                billings = billings.Where(b => b.ConsumerId == consumerId.Value);
            }

            if (!string.IsNullOrEmpty(billingStatus) && billingStatus != "All")
            {
                billings = billings.Where(b => b.Status == billingStatus);
            }

            var billingsList = await billings.ToListAsync();

            // Join payments to billings for filtering payments by billing date (not payment date)
            var paymentsQuery = _context.Payments.Include(p => p.Consumer).AsQueryable();

            // Filter payments based on linked billing date and other filters
            paymentsQuery = from p in paymentsQuery
                            join b in _context.Billings on p.BillingId equals b.Id
                            where (!startDate.HasValue || b.BillingDate >= startDate.Value)
                               && (!endDate.HasValue || b.BillingDate <= endDate.Value)
                               && (!consumerId.HasValue || b.ConsumerId == consumerId.Value)
                            select p;

            var paymentsList = await paymentsQuery.ToListAsync();

            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
            var totalCubicMeterUsed = billingsList.Sum(b => b.CubicMeterUsed);
            var pdfBytes = ReportPdfService.GenerateReport(billingsList, paymentsList, logoPath, totalCubicMeterUsed);

            var filename = string.IsNullOrEmpty(selectedMonth)
                ? "FilteredReport.pdf"
                : $"Report_{selectedMonth}.pdf";

            return File(pdfBytes, "application/pdf", filename);
        }

    }
}
