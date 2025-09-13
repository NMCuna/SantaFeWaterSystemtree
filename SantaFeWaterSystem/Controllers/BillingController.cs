using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Helpers;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.Settings;
using SantaFeWaterSystem.ViewModels;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using X.PagedList;


namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class BillingController(
        ApplicationDbContext context,
        PermissionService permissionService,
        ISemaphoreSmsService smsService,     
        IOptions<SemaphoreSettings> semaphoreOptions,
        IWebHostEnvironment env,
        ISmsQueue smsQueue,
        AuditLogService audit,
          IEmailSender emailSender
    ) : BaseController(permissionService, context, audit)
    {
        private readonly ISemaphoreSmsService _smsService = smsService;        
        private readonly SemaphoreSettings _semaphoreSettings = semaphoreOptions.Value;
        private readonly IWebHostEnvironment _env = env;
        private readonly ISmsQueue _smsQueue = smsQueue;
        private readonly IEmailSender _emailSender = emailSender;




        // ================== INDEX BILLING LIST ==================       

        // GET: Billing    
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Index(string searchTerm, string statusFilter, int? selectedMonth, int? selectedYear, int page = 1)
    {
        int pageSize = 7;

        var query = _context.Billings
            .Include(b => b.Consumer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(b =>
                b.Consumer.FirstName.Contains(searchTerm) ||
                b.Consumer.LastName.Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(b => b.Status == statusFilter);
        }
            // Filter by Month and Year if selected
            if (selectedMonth.HasValue && selectedYear.HasValue)
            {
                query = query.Where(b =>
                    b.BillingDate.Month == selectedMonth.Value &&
                    b.BillingDate.Year == selectedYear.Value);
            }
            else
            {
                // Default to current month if no filters are applied
                query = query.Where(b =>
                    b.BillingDate.Month == DateTime.Now.Month &&
                    b.BillingDate.Year == DateTime.Now.Year);
            }

            var billingsPaged = await query
         .OrderByDescending(b => b.BillingDate)
         .ToPagedListAsync(page, pageSize);

            // Apply penalty and audit on just the current page
            foreach (var bill in billingsPaged)
            {
                var consumer = bill.Consumer;

                var rateRecord = await _context.Rates
                    .Where(r => r.AccountType == consumer.AccountType && r.EffectiveDate <= bill.BillingDate)
                    .OrderByDescending(r => r.EffectiveDate)
                    .FirstOrDefaultAsync();

                decimal penaltyFromRate = rateRecord?.PenaltyAmount ?? 10m;
                decimal originalPenalty = bill.Penalty;

                if (bill.DueDate < DateTime.Today && bill.Status != "Paid")
                {
                    bill.Penalty = penaltyFromRate;

                    if (originalPenalty != penaltyFromRate)
                    {
                        _context.AuditTrails.Add(new AuditTrail
                        {
                            Action = "PenaltyApplied",
                            PerformedBy = User.Identity?.Name ?? "System",
                            Timestamp = DateTime.Now,
                            Details = $"Applied ₱{penaltyFromRate} penalty to BillNo {bill.BillNo} (Consumer: {consumer.FirstName} {consumer.LastName})"
                        });
                    }
                }
                else
                {
                    bill.Penalty = 0m;
                }

                decimal additionalFees = bill.AdditionalFees ?? 0m;
                bill.TotalAmount = bill.AmountDue + bill.Penalty + additionalFees;
            }

            _context.Billings.UpdateRange(billingsPaged);
            await _context.SaveChangesAsync();

            ViewBag.CurrentSearchTerm = searchTerm;
            ViewBag.CurrentStatusFilter = statusFilter;
            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;
            return View(billingsPaged);

        }



        // ================== NOTIFY BUTTON CAN BE USE IF USER IS OVERDUE ==================
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Notify(
            int id,
            int page = 1,
            string searchTerm = null,
            string statusFilter = null,
            int? selectedMonth = null,
            int? selectedYear = null)
        {
            var billing = await _context.Billings
                .Include(b => b.Consumer)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (billing != null)
            {
                bool isOverdue = DateTime.Now.Date > billing.DueDate.Date;

                // Create In-App Notification
                var notif = new Notification
                {
                    ConsumerId = billing.ConsumerId,
                    Title = isOverdue ? "💧 Overdue Water Bill" : "💧 Water Billing Notification",
                    Message = isOverdue
                        ? $"Hello {billing.Consumer.FirstName}, your water bill (Bill No: {billing.BillNo}) dated {billing.BillingDate:yyyy-MM-dd} is now **overdue** since {billing.DueDate:yyyy-MM-dd}. Please pay ₱{billing.TotalAmount:N2} immediately to avoid disconnection."
                        : $"Hello {billing.Consumer.FirstName}, your water bill (Bill No: {billing.BillNo}) dated {billing.BillingDate:yyyy-MM-dd} is due on {billing.DueDate:yyyy-MM-dd}. Amount due: ₱{billing.TotalAmount:N2}. Please settle it promptly to avoid disconnection.",
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notif);

                // 🔔 Push Notification
                var user = billing.Consumer.User;
                if (user != null)
                {
                    var subscriptions = await _context.UserPushSubscriptions
                        .Where(s => s.UserId == user.Id)
                        .ToListAsync();

                    var vapidAuth = new VapidAuthentication(
                        "BA_B1RL8wfVkIA7o9eZilYNt7D0_CbU5zsvqCZUFcCnVeqFr6a9BPxHPtWlNNgllEkEqk6jcRgp02ypGhGO3gZI",
                        "0UqP8AfB9hFaQhm54rEabEwlaCo44X23BO6ID8n7E_U")
                    {
                        Subject = "mailto:cunanicolemichael@gmail.com"
                    };

                    var pushClient = new PushServiceClient
                    {
                        DefaultAuthentication = vapidAuth
                    };

                    string pushPayload = JsonSerializer.Serialize(new
                    {
                        title = notif.Title,
                        body = isOverdue
                            ? "Your water bill is overdue. Please pay now to avoid disconnection."
                            : "New water bill available. Please check your account."
                    });

                    foreach (var sub in subscriptions)
                    {
                        var subscription = new PushSubscription
                        {
                            Endpoint = sub.Endpoint,
                            Keys = new Dictionary<string, string>
                    {
                        { "p256dh", sub.P256DH },
                        { "auth", sub.Auth }
                    }
                        };

                        try
                        {
                            await pushClient.RequestPushMessageDeliveryAsync(subscription, new PushMessage(pushPayload));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Push Error] {ex.Message}");
                        }
                    }
                }

                // 📧 Email Notification
                if (!string.IsNullOrWhiteSpace(billing.Consumer.Email))
                {
                    var emailSubject = isOverdue
                        ? $"[Overdue] Water Bill (Bill No: {billing.BillNo}) - Due {billing.DueDate:MMMM d}"
                        : $"Water Bill (Bill No: {billing.BillNo}) - Due {billing.DueDate:MMMM d}";

                    var emailBody = $@"
                <p>Hello <strong>{billing.Consumer.FullName}</strong>,</p>
                <p>Your water bill for <strong>{billing.BillingDate:MMMM yyyy}</strong> is {(isOverdue ? "<span style='color:red;'>OVERDUE</span>" : "now available")}.</p>
                <p><strong>Bill No:</strong> {billing.BillNo}<br/>
                <strong>Billing Date:</strong> {billing.BillingDate:MMMM d, yyyy}<br/>
                <strong>Amount Due:</strong> ₱{billing.TotalAmount:N2}<br/>
                <strong>Due Date:</strong> {billing.DueDate:MMMM d, yyyy}</p>
                <p>Please settle {(isOverdue ? "immediately" : "promptly")} to avoid penalties or disconnection.</p>
                <p>Thank you,<br/>Santa Fe Water System</p>";

                    try
                    {
                        await _emailSender.SendEmailAsync(billing.Consumer.Email, emailSubject, emailBody);

                        _context.EmailLogs.Add(new EmailLog
                        {
                            ConsumerId = billing.Consumer.Id,
                            EmailAddress = billing.Consumer.Email,
                            Subject = emailSubject,
                            Message = emailBody,
                            IsSuccess = true,
                            SentAt = DateTime.Now
                        });
                    }
                    catch (Exception ex)
                    {
                        _context.EmailLogs.Add(new EmailLog
                        {
                            ConsumerId = billing.Consumer.Id,
                            EmailAddress = billing.Consumer.Email,
                            Subject = emailSubject,
                            Message = emailBody,
                            IsSuccess = false,
                            SentAt = DateTime.Now,
                            ResponseMessage = ex.Message
                        });
                    }
                }

                // 📝 Audit Log
                _context.AuditTrails.Add(new AuditTrail
                {
                    Action = "Notify",
                    PerformedBy = GetCurrentUsername(),
                    Details = $"Sent {(isOverdue ? "overdue" : "billing")} notice to Consumer ID {billing.ConsumerId}.",
                    Timestamp = DateTime.Now
                });

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✅ Notification sent for Billing ID: {id}.";
            }
            else
            {
                TempData["Error"] = $"❌ Billing ID {id} not found.";
            }

            // Redirect back to same page & filters
            return RedirectToAction("Index", new
            {
                page,
                searchTerm,
                statusFilter,
                selectedMonth,
                selectedYear
            });
        }




        // ================== GetCurrentUsername ==================
        private string GetCurrentUsername()
        {
            return User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        }



        // ================== ExportSelectedToPdf ==================

        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportSelectedToPdf(string selectedBillingIds)
        {
            if (string.IsNullOrWhiteSpace(selectedBillingIds))
                return BadRequest("No billing IDs selected.");

            var ids = selectedBillingIds.Split(',')
                .Where(id => int.TryParse(id, out _))
                .Select(int.Parse)
                .ToList();

            if (!ids.Any())
                return BadRequest("Invalid billing ID list.");

            var billings = await _context.Billings
                .Include(b => b.Consumer)
                .Where(b => ids.Contains(b.Id))
                .ToListAsync();

            if (!billings.Any())
                return NotFound("No billing records found for the selected IDs.");

            var model = new MonthlyBillingSummaryViewModel
            {
                Month = DateTime.Now.Month, // Optional, since these are mixed
                Year = DateTime.Now.Year,
                Billings = billings
            };

            var document = new MonthlyBillingPdfDocument(model);
            var pdfStream = new MemoryStream();
            document.GeneratePdf(pdfStream);
            pdfStream.Position = 0;

            string filename = $"SelectedBillingExport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(pdfStream, "application/pdf", filename);
        }



        // ================== ExportMonthlySummaryPdf ==================

        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> ExportMonthlySummaryPdf(int month, int year)
        {
            if (month < 1 || month > 12 || year < 2000 || year > DateTime.Now.Year)
                return BadRequest("Invalid month or year.");

            var billings = await _context.Billings
                .Include(b => b.Consumer)
                .Where(b => b.BillingDate.Month == month && b.BillingDate.Year == year)
                .ToListAsync();

            if (!billings.Any())
                return NotFound("No billing records found for the selected month and year.");

            var model = new MonthlyBillingSummaryViewModel
            {
                Month = month,
                Year = year,
                Billings = billings
            };

            var document = new MonthlyBillingPdfDocument(model);
            var pdfStream = new MemoryStream();
            document.GeneratePdf(pdfStream);
            pdfStream.Position = 0;

            string filename = $"BillingSummary_{year}_{month}.pdf";
            return File(pdfStream, "application/pdf", filename);
        }







        // ================== CREATE NEW BILLING ==================

        // GET: Billing/Create
        [Authorize(Roles = "Admin,Staff")]
        public IActionResult Create()
        {
            DateTime now = DateTime.Now;
            int currentMonth = now.Month;
            int currentYear = now.Year;

            // Get consumers who already have an active bill this month
            var consumersWithActiveBills = _context.Billings
                .Where(b => b.BillingDate.Month == currentMonth &&
                            b.BillingDate.Year == currentYear &&
                            b.DueDate >= now)
                .Select(b => b.ConsumerId)
                .Distinct()
                .ToList();

            var eligibleConsumers = _context.Consumers
                .Where(c => !consumersWithActiveBills.Contains(c.Id))
                .ToList();

            var billingDate = now.Date;
            var dueDate = billingDate.AddDays(20);

            var viewModel = new BillingFormViewModel
            {
                Billing = new Billing
                {
                    BillingDate = billingDate,
                    DueDate = dueDate,
                    // BillNo will be set later during POST
                },
                Consumers = eligibleConsumers
            };

            return View(viewModel);
        }



        // POST: AdminDashboard/Billing/Create
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BillingFormViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                await LoadEligibleConsumersAsync(viewModel);
                return View(viewModel);
            }

            var billing = viewModel.Billing;

            // ===== Get latest billing and compute usage =====
            var lastBilling = await _context.Billings
                .Where(b => b.ConsumerId == billing.ConsumerId)
                .OrderByDescending(b => b.BillingDate)
                .FirstOrDefaultAsync();

            billing.PreviousReading = lastBilling?.PresentReading ?? 0m;
            billing.CubicMeterUsed = billing.PresentReading - billing.PreviousReading;

            if (billing.CubicMeterUsed < 0)
            {
                ModelState.AddModelError("", "Present Reading must be greater than or equal to Previous Reading.");
                await LoadEligibleConsumersAsync(viewModel);
                return View(viewModel);
            }

            var consumer = await _context.Consumers.FindAsync(billing.ConsumerId);
            if (consumer == null)
            {
                ModelState.AddModelError("", "Consumer not found.");
                await LoadEligibleConsumersAsync(viewModel);
                return View(viewModel);
            }

            // ===== Get rate =====
            var rateRecord = await _context.Rates
                .Where(r => r.AccountType == consumer.AccountType && r.EffectiveDate <= billing.BillingDate)
                .OrderByDescending(r => r.EffectiveDate)
                .FirstOrDefaultAsync();

            if (rateRecord == null)
            {
                ModelState.AddModelError("", "No rate defined for the consumer's account type at the billing date.");
                await LoadEligibleConsumersAsync(viewModel);
                return View(viewModel);
            }

            decimal rate = rateRecord.RatePerCubicMeter;
            var actualUsage = billing.CubicMeterUsed;
            var chargeableUsage = actualUsage < 10 ? 10 : actualUsage;

            billing.CubicMeterUsed = actualUsage;
            billing.AmountDue = rate * chargeableUsage;
            billing.Remarks = actualUsage < 10 ? "Minimum charge applied" : null;
            billing.DueDate = billing.DueDate == default ? billing.BillingDate.AddDays(20) : billing.DueDate;
            billing.Status = string.IsNullOrWhiteSpace(billing.Status) ? "Unpaid" : billing.Status;

            billing.Penalty = (DateTime.Now > billing.DueDate && billing.Status != "Paid")
                ? (rateRecord.PenaltyAmount > 0 ? rateRecord.PenaltyAmount : 10m)
                : 0m;

            decimal additionalFees = billing.AdditionalFees ?? 0m;
            billing.TotalAmount = billing.AmountDue + billing.Penalty + additionalFees;

            billing.BillNo = await GenerateBillNoForConsumerAsync(billing.ConsumerId);

            // ===== Stage core entities (no Save yet) =====
            _context.Billings.Add(billing);

            _context.Notifications.Add(new Notification
            {
                ConsumerId = billing.ConsumerId,
                Title = "New Water Bill",
                Message = $"Hello {consumer.FirstName}, your bill (Bill No: {billing.BillNo}) dated {billing.BillingDate:yyyy-MM-dd} is due on {billing.DueDate:yyyy-MM-dd}. Total amount: ₱{billing.TotalAmount:N2}. Please settle it promptly to avoid disconnection.",
                CreatedAt = DateTime.Now
            });

            _context.AuditTrails.Add(new AuditTrail
            {
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Action = "Created Billing",
                Timestamp = DateTime.Now,
                Details = $"Created billing for Consumer ID: {billing.ConsumerId}, Bill No: {billing.BillNo}, " +
                          $"Billing Date: {billing.BillingDate:MM/dd/yyyy}, Previous Reading: {billing.PreviousReading}, " +
                          $"Present Reading: {billing.PresentReading}, Cubic Meter Used: {billing.CubicMeterUsed}, " +
                          $"Amount Due: ₱{billing.AmountDue:N2}, Penalty: ₱{billing.Penalty:N2}, " +
                          $"Additional Fees: ₱{billing.AdditionalFees ?? 0:N2}, Total: ₱{billing.TotalAmount:N2}, " +
                          $"Due Date: {billing.DueDate:MM/dd/yyyy}, Status: {billing.Status}"
            });

            // Create a BillNotification entity and stage it (navigation to billing so EF wires FKs)
            BillNotification billNotif = null;
            var user = await _context.Users.Include(u => u.Consumer)
                .FirstOrDefaultAsync(u => u.Consumer != null && u.Consumer.Id == billing.ConsumerId);

            if (user != null)
            {
                billNotif = new BillNotification
                {
                    Billing = billing,     // use navigation property
                    ConsumerId = billing.ConsumerId,
                    IsNotified = false
                };
                _context.BillNotifications.Add(billNotif);
            }

            // ===== Persist core records (1st save) =====
            await _context.SaveChangesAsync();
            // At this point: billing.Id exists and in-app notification + audit + billNotif are stored.

            // ===== Non-critical external operations (best-effort) =====

            // 1) Push notifications (parallelized)
            if (user != null)
            {
                var subscriptions = await _context.UserPushSubscriptions
                    .Where(s => s.UserId == user.Id)
                    .ToListAsync();

                if (subscriptions.Any())
                {
                    var vapidAuth = new VapidAuthentication(
                     "BA_B1RL8wfVkIA7o9eZilYNt7D0_CbU5zsvqCZUFcCnVeqFr6a9BPxHPtWlNNgllEkEqk6jcRgp02ypGhGO3gZI",
                     "0UqP8AfB9hFaQhm54rEabEwlaCo44X23BO6ID8n7E_U")
                    {
                        Subject = "mailto:cunanicolemichael@gmail.com"
                    };

                    var pushClient = new Lib.Net.Http.WebPush.PushServiceClient
                    {
                        DefaultAuthentication = vapidAuth
                    };

                    var pushTasks = subscriptions.Select(sub => Task.Run(async () =>
                    {
                        try
                        {
                            var subscription = new Lib.Net.Http.WebPush.PushSubscription
                            {
                                Endpoint = sub.Endpoint,
                                Keys = new Dictionary<string, string>
                 {
                     { "p256dh", sub.P256DH },
                     { "auth", sub.Auth }
                 }
                            };

                            var payload = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                title = "📢 New Water Bill",
                                body = $"Amount Due: ₱{billing.AmountDue:N2} - Due {billing.DueDate:MMM dd}"
                            });

                            await pushClient.RequestPushMessageDeliveryAsync(subscription,
                                new Lib.Net.Http.WebPush.PushMessage(payload));

                            return true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Push Error] {ex.Message}");
                            return false;
                        }
                    })).ToArray();

                    bool anyPushSuccess = false;
                    try
                    {
                        var results = await Task.WhenAll(pushTasks);
                        anyPushSuccess = results.Any(r => r);
                    }
                    catch
                    {
                        // Task.WhenAll can throw if something unexpected occurs — we already handle exceptions inside tasks.
                        anyPushSuccess = false;
                    }

                    if (anyPushSuccess && billNotif != null)
                    {
                        billNotif.IsNotified = true; // tracked entity — will be persisted on final save
                    }
                }
            }

            // 2) SMS (best-effort) — prepare log to save later
            if (!string.IsNullOrWhiteSpace(consumer.ContactNumber))
            {
                try
                {
                    var smsMessage = $"Hello {consumer.FullName}, your water bill (#{billing.BillNo}) for {billing.BillingDate:MMMM yyyy} is ₱{billing.TotalAmount:N2}. Due on {billing.DueDate:MMMM d}. – Santa Fe Water System";
                    var smsResult = await _smsService.SendSmsAsync(consumer.ContactNumber, smsMessage);

                    _context.SmsLogs.Add(new SmsLog
                    {
                        ConsumerId = consumer.Id,
                        ContactNumber = consumer.ContactNumber,
                        Message = smsMessage,
                        IsSuccess = smsResult.success,
                        SentAt = DateTime.Now,
                        ResponseMessage = smsResult.response
                    });

                    TempData["SmsStatus"] = smsResult.success ? "✅ SMS sent successfully." : $"❌ SMS failed: {smsResult.response}";
                }
                catch (Exception ex)
                {
                    // If SMS sending throws unexpectedly, log the failure to DB
                    _context.SmsLogs.Add(new SmsLog
                    {
                        ConsumerId = consumer.Id,
                        ContactNumber = consumer.ContactNumber,
                        Message = $"(Failure) Could not send SMS for billing #{billing.BillNo}",
                        IsSuccess = false,
                        SentAt = DateTime.Now,
                        ResponseMessage = ex.Message
                    });
                    TempData["SmsStatus"] = $"❌ SMS failed: {ex.Message}";
                }
            }

            // 3) Email (best-effort) — prepare log to save later
            if (!string.IsNullOrWhiteSpace(consumer.Email))
            {
                var emailSubject = $"Your Water Bill (Bill No: {billing.BillNo}) - Due {billing.DueDate:MMMM d}";
                var emailBody = $@"
     <p>Hello <strong>{consumer.FullName}</strong>,</p>
     <p>Your water bill for <strong>{billing.BillingDate:MMMM yyyy}</strong> is now available.</p>
     <p><strong>Bill No:</strong> {billing.BillNo}<br/>
     <strong>Billing Date:</strong> {billing.BillingDate:MMMM d, yyyy}<br/>
     <strong>Amount Due:</strong> ₱{billing.TotalAmount:N2}<br/>
     <strong>Due Date:</strong> {billing.DueDate:MMMM d, yyyy}</p>
     <p>Please settle it promptly to avoid penalties or disconnection.</p>
     <p>Thank you,<br/>Santa Fe Water System</p>";

                try
                {
                    await _emailSender.SendEmailAsync(consumer.Email, emailSubject, emailBody);

                    _context.EmailLogs.Add(new EmailLog
                    {
                        ConsumerId = consumer.Id,
                        EmailAddress = consumer.Email,
                        Subject = emailSubject,
                        Message = emailBody,
                        IsSuccess = true,
                        SentAt = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    _context.EmailLogs.Add(new EmailLog
                    {
                        ConsumerId = consumer.Id,
                        EmailAddress = consumer.Email,
                        Subject = emailSubject,
                        Message = emailBody,
                        IsSuccess = false,
                        SentAt = DateTime.Now,
                        ResponseMessage = ex.Message
                    });
                }
            }

            // ===== Final Save: persist logs and any updates (billNotif.IsNotified) =====
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Billing successfully created and notifications processed (in-app guaranteed).";
            return RedirectToAction(nameof(Index));
        }







        // ================== LoadEligibleConsumersAsync ==================

        // Helper method to populate consumer dropdown
        private async Task LoadEligibleConsumersAsync(BillingFormViewModel viewModel)
        {
            DateTime now = DateTime.Now;
            int currentMonth = now.Month;
            int currentYear = now.Year;

            var consumersWithActiveBills = await _context.Billings
                .Where(b => b.BillingDate.Month == currentMonth &&
                            b.BillingDate.Year == currentYear &&
                            b.DueDate >= now)
                .Select(b => b.ConsumerId)
                .Distinct()
                .ToListAsync();

            viewModel.Consumers = await _context.Consumers
                .Where(c => !consumersWithActiveBills.Contains(c.Id))
                .ToListAsync();
        }

        // Helper to generate unique BillNo per consumer
        private async Task<string> GenerateBillNoForConsumerAsync(int consumerId)
        {
            var lastBill = await _context.Billings
                .Where(b => b.ConsumerId == consumerId)
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync();

            int newBillNumber = 1;

            if (lastBill != null && int.TryParse(lastBill.BillNo, out int lastBillNo))
            {
                newBillNumber = lastBillNo + 1;
            }

            return newBillNumber.ToString("D4"); // Example: 0001, 0002, etc.
        }




        // ================== DETAILS BILLING ==================

        // GET: Billing/Details/5
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Details(int? id, int? page, string? search, string? status)
        {
            if (id == null)
                return NotFound();

            var billing = await _context.Billings
                .Include(b => b.Consumer)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (billing == null)
                return NotFound();

            // Audit trail
            _context.AuditTrails.Add(new AuditTrail
            {
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Action = "Viewed Billing Details",
                Timestamp = DateTime.Now,
                Details = $"Viewed Billing ID: {billing.Id}, Bill No: {billing.BillNo}, Consumer: {billing.Consumer?.FirstName} {billing.Consumer?.LastName}"
            });
            await _context.SaveChangesAsync();

            // Keep the state for Back button
            ViewData["Page"] = page;
            ViewData["Search"] = search;
            ViewData["Status"] = status;

            return View(billing);
        }




        // ================== EDIT BILLING ==================

        // GET: Billing/Edit/5
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Edit(int id, int? page, string? search, string? status)
        {
            var billing = await _context.Billings
                .Include(b => b.Consumer)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (billing == null) return NotFound();

            var consumer = billing.Consumer;

            // 🔑 Find the applicable rate
            var rateRecord = await _context.Rates
                .Where(r => r.AccountType == consumer.AccountType && r.EffectiveDate <= billing.BillingDate)
                .OrderByDescending(r => r.EffectiveDate)
                .FirstOrDefaultAsync();

            // Send it to the view
            ViewBag.RatePerCubic = rateRecord?.RatePerCubicMeter ?? 20; // fallback if none
            ViewBag.Consumers = await _context.Consumers.ToListAsync();

            // Save current state to return to Index
            ViewData["Page"] = page;
            ViewData["Search"] = search;
            ViewData["Status"] = status;

            return View(billing);
        }

        //post edit
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Billing billing, int? page, string? search, string? status)
        {
            if (id != billing.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Consumers = _context.Consumers.ToList();
                return View(billing);
            }

            var existing = await _context.Billings
                .Include(b => b.Consumer)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (existing == null) return NotFound();

            try
            {
                // ===== Keep old values for audit =====
                var oldReading = existing.PresentReading;
                var oldAmountDue = existing.AmountDue;
                var oldPenalty = existing.Penalty;
                var oldAdditionalFees = existing.AdditionalFees;
                var oldTotalAmount = existing.TotalAmount;
                var oldStatus = existing.Status;
                var oldDueDate = existing.DueDate;

                // ===== Recalculate like Create =====
                existing.PresentReading = billing.PresentReading;
                existing.PreviousReading = billing.PreviousReading;

                existing.CubicMeterUsed = existing.PresentReading - existing.PreviousReading;
                if (existing.CubicMeterUsed < 0)
                {
                    ModelState.AddModelError("", "Present Reading must be greater than or equal to Previous Reading.");
                    ViewBag.Consumers = _context.Consumers.ToList();
                    return View(existing);
                }

                var consumer = await _context.Consumers.FindAsync(existing.ConsumerId);
                if (consumer == null)
                {
                    ModelState.AddModelError("", "Consumer not found.");
                    ViewBag.Consumers = _context.Consumers.ToList();
                    return View(existing);
                }

                // Get latest rate
                var rateRecord = await _context.Rates
                    .Where(r => r.AccountType == consumer.AccountType && r.EffectiveDate <= existing.BillingDate)
                    .OrderByDescending(r => r.EffectiveDate)
                    .FirstOrDefaultAsync();

                if (rateRecord == null)
                {
                    ModelState.AddModelError("", "No rate defined for this consumer type.");
                    ViewBag.Consumers = _context.Consumers.ToList();
                    return View(existing);
                }

                var rate = rateRecord.RatePerCubicMeter;
                var actualUsage = existing.CubicMeterUsed;
                var chargeableUsage = actualUsage < 10 ? 10 : actualUsage;

                existing.AmountDue = rate * chargeableUsage;
                existing.Remarks = actualUsage < 10 ? "Minimum charge applied" : null;
                existing.AdditionalFees = billing.AdditionalFees ?? 0;
                existing.Status = billing.Status;

                // Due date rule
                if (billing.DueDate == default || billing.DueDate < existing.BillingDate)
                    existing.DueDate = existing.BillingDate.AddDays(20);
                else
                    existing.DueDate = billing.DueDate;

                // Penalty rule
                existing.Penalty = (DateTime.Now > existing.DueDate && existing.Status != "Paid")
                    ? (rateRecord.PenaltyAmount > 0 ? rateRecord.PenaltyAmount : 10m)
                    : 0;

                existing.TotalAmount = existing.AmountDue + existing.Penalty + existing.AdditionalFees.GetValueOrDefault();

                _context.Update(existing);

                // ===== Audit trail =====
                _context.AuditTrails.Add(new AuditTrail
                {
                    PerformedBy = User.Identity?.Name ?? "Unknown",
                    Action = "Edited Billing",
                    Timestamp = DateTime.Now,
                    Details = $"Billing ID: {existing.Id}, " +
                              $"Present Reading: {oldReading} → {existing.PresentReading}, " +
                              $"Amount Due: ₱{oldAmountDue:N2} → ₱{existing.AmountDue:N2}, " +
                              $"Penalty: ₱{oldPenalty:N2} → ₱{existing.Penalty:N2}, " +
                              $"Additional Fees: ₱{oldAdditionalFees:N2} → ₱{existing.AdditionalFees:N2}, " +
                              $"Total: ₱{oldTotalAmount:N2} → ₱{existing.TotalAmount:N2}, " +
                              $"Status: {oldStatus} → {existing.Status}, " +
                              $"Due Date: {oldDueDate:MM/dd/yyyy} → {existing.DueDate:MM/dd/yyyy}"
                });

                // ===== In-app notification =====
                _context.Notifications.Add(new Notification
                {
                    ConsumerId = existing.ConsumerId,
                    Title = "📢 Bill Updated",
                    Message = $"Hello {consumer.FullName},\n\n" +
                              $"We’ve updated your bill (Bill No: {existing.BillNo}). " +
                              $"The new total is ₱{existing.TotalAmount:N2}. " +
                              $"This update was made to ensure your billing information is accurate.\n\n" +
                              $"We sincerely apologize for any inconvenience this may have caused. " +
                              $"Thank you for your understanding and continued trust in the Santa Fe Water System.",
                    CreatedAt = DateTime.Now
                });

                // ===== Push notification (best-effort) =====
                var user = await _context.Users.Include(u => u.Consumer)
                    .FirstOrDefaultAsync(u => u.Consumer != null && u.Consumer.Id == existing.ConsumerId);

                if (user != null)
                {
                    var subscriptions = await _context.UserPushSubscriptions
                        .Where(s => s.UserId == user.Id)
                        .ToListAsync();

                    if (subscriptions.Any())
                    {
                        try
                        {
                            var vapidAuth = new VapidAuthentication(
                             "BA_B1RL8wfVkIA7o9eZilYNt7D0_CbU5zsvqCZUFcCnVeqFr6a9BPxHPtWlNNgllEkEqk6jcRgp02ypGhGO3gZI",
                             "0UqP8AfB9hFaQhm54rEabEwlaCo44X23BO6ID8n7E_U")
                            {
                                Subject = "mailto:cunanicolemichael@gmail.com"
                            };

                            var pushClient = new Lib.Net.Http.WebPush.PushServiceClient
                            {
                                DefaultAuthentication = vapidAuth
                            };

                            var payload = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                title = "📢 Bill Updated",
                                body = $"Bill #{existing.BillNo} updated. Total: ₱{existing.TotalAmount:N2}"
                            });

                            foreach (var sub in subscriptions)
                            {
                                var subscription = new Lib.Net.Http.WebPush.PushSubscription
                                {
                                    Endpoint = sub.Endpoint,
                                    Keys = new Dictionary<string, string>
                            {
                                { "p256dh", sub.P256DH },
                                { "auth", sub.Auth }
                            }
                                };

                                try
                                {
                                    await pushClient.RequestPushMessageDeliveryAsync(subscription,
                                        new Lib.Net.Http.WebPush.PushMessage(payload));
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[Push Error] {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Push Setup Error] {ex.Message}");
                        }
                    }
                }

                // NOTE: No SMS, no Email → log why
                _context.EmailLogs.Add(new EmailLog
                {
                    ConsumerId = existing.ConsumerId,
                    EmailAddress = consumer.Email,
                    Subject = "Bill Edit",
                    Message = "Email skipped intentionally for bill edits.",
                    IsSuccess = false,
                    SentAt = DateTime.Now,
                    ResponseMessage = "Skipped by design (edit uses only app + push)."
                });

                _context.SmsLogs.Add(new SmsLog
                {
                    ConsumerId = existing.ConsumerId,
                    ContactNumber = consumer.ContactNumber,
                    Message = "SMS skipped intentionally for bill edits.",
                    IsSuccess = false,
                    SentAt = DateTime.Now,
                    ResponseMessage = "Skipped by design (edit uses only app + push)."
                });

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ Billing updated successfully (in-app + push notification sent).";
                // Redirect to Index with previous page + filters
                return RedirectToAction(nameof(Index), new { page, search, status });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Billings.Any(e => e.Id == id))
                    return NotFound();
                else
                    throw;
            }
        }






        // ================== DELETE BILLING ==================

        // GET: Billing/Delete/5
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Delete(int? id, int page = 1, string search = null, string status = null, int? selectedMonth = null, int? selectedYear = null)
        {
            if (id == null) return NotFound();

            var billing = await _context.Billings
                .Include(b => b.Consumer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (billing == null) return NotFound();

            ViewData["Page"] = page;
            ViewData["Search"] = search;
            ViewData["Status"] = status;
            ViewData["SelectedMonth"] = selectedMonth;
            ViewData["SelectedYear"] = selectedYear;

            return View(billing);
        }


        //Post:delete
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost, ActionName("DeleteConfirmed")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, int page = 1, string search = null, string status = null, int? selectedMonth = null, int? selectedYear = null)
        {
            var billing = await _context.Billings
                .Include(b => b.Consumer)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (billing == null) return NotFound();

            _context.Billings.Remove(billing);

            //  Audit trail log for deletion
            _context.AuditTrails.Add(new AuditTrail
            {
                PerformedBy = User.Identity?.Name ?? "Unknown",
                Action = "Deleted Billing",
                Timestamp = DateTime.Now,
                Details = $"Deleted Billing ID: {billing.Id}, Bill No: {billing.BillNo}, " +
                          $"Consumer: {billing.Consumer?.FirstName} {billing.Consumer?.LastName}, " +
                          $"Amount Due: ₱{billing.AmountDue:N2}, Total: ₱{billing.TotalAmount:N2}, " +
                          $"Status: {billing.Status}, Billing Date: {billing.BillingDate:MM/dd/yyyy}"
            });

            await _context.SaveChangesAsync();

            // Redirect back to the same page with filters
            return RedirectToAction(nameof(Index), new
            {
                page,
                searchTerm = search,
                statusFilter = status,
                selectedMonth,
                selectedYear
            });
        }






        // ================== SEND SMS ==================

        [HttpPost]
        public async Task<IActionResult> SendSms([FromBody] List<int> billingIds)
        {
            var antiForgery = HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            await antiForgery.ValidateRequestAsync(HttpContext);

            if (billingIds == null || billingIds.Count == 0)
                return BadRequest("No billing IDs provided.");

            var billings = await _context.Billings
                .Include(b => b.Consumer)
                .Where(b => billingIds.Contains(b.Id))
                .ToListAsync();

            int sentCount = 0;
            List<string> failed = new();

            foreach (var billing in billings)
            {
                var consumer = billing.Consumer;
                var number = consumer?.ContactNumber?.Trim();
                if (string.IsNullOrWhiteSpace(number)) continue;

                string amount = billing.TotalAmount.ToString("C", new CultureInfo("en-PH"));
                string message = $"Hello {consumer.FirstName} {consumer.LastName}, this is a reminder from Santa Fe Water System. " +
                   $"Your water bill for {billing.BillingDate:MMMM yyyy} is {amount}, due on {billing.DueDate:MMMM dd, yyyy}.";


                bool isSuccess = false;
                string response = "";

                if (_env.IsDevelopment())
                {
                    var result = await _smsService.SendSmsAsync(number, message);
                    isSuccess = result.success;
                    response = result.response;

                    if (isSuccess) sentCount++;
                    else failed.Add($"{number}: {response}");
                }
                else
                {
                    await _smsQueue.QueueMessageAsync(number, message, consumer.Id);
                    isSuccess = true;
                    response = "Queued for sending.";
                    sentCount++;
                } 

                // Log to database
                _context.SmsLogs.Add(new SmsLog
                {
                    ConsumerId = consumer.Id,
                    ContactNumber = number,
                    Message = message,
                    IsSuccess = isSuccess,
                    SentAt = DateTime.Now,
                    ResponseMessage = response
                });
            }

            await _context.SaveChangesAsync(); // Save logs

            return Ok(new
            {
                success = true,
                messagesSent = sentCount,
                failedRecipients = failed
            });
        }
    }
}
