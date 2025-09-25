using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Models.ViewModels;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.ViewModels;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using SantaFeWaterSystem.Filters;



namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "User")]
    [RequirePrivacyAgreement]
    public class UserController(ApplicationDbContext context, IWebHostEnvironment environment, IPasswordHasher<User> passwordHasher, AuditLogService audit, PasswordPolicyService passwordPolicyService, IEmailSender emailSender) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _environment = environment;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly AuditLogService _audit = audit;
        private readonly PasswordPolicyService _passwordPolicyService = passwordPolicyService;
        private readonly IEmailSender _emailSender = emailSender;




        //////////////////////////////////
        //          DASHBOARD           //
        //////////////////////////////////


        // ================== SHOW THE DASHBOARD VIEW ==================

        // GET: /User/Dashboard
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Dashboard()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized("User ID not found or invalid.");

            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null)
            {
                // Log it
                await _audit.LogAsync(
                    "Unlinked Login",
                    $"User {User.Identity?.Name} logged in but does not have linked consumer information."
                );

                // Show user-friendly page instead of plain text
                return View("NotLinked");
            }

            ViewBag.IsMfaEnabled = consumer.User?.IsMfaEnabled ?? false;

            // Get latest 5 bills (can be changed if needed)
            var recentBills = await _context.Billings
                .Where(b => b.ConsumerId == consumer.Id)
                .OrderByDescending(b => b.BillingDate)
                .Take(5000)
                .ToListAsync();

            var billIds = recentBills.Select(b => b.Id).ToList();
            var relatedPayments = await _context.Payments
                .Where(p => billIds.Contains(p.BillingId))
                .ToListAsync();

            foreach (var bill in recentBills)
            {
                var paymentsForBill = relatedPayments.Where(p => p.BillingId == bill.Id);
                bool hasVerified = paymentsForBill.Any(p => p.IsVerified);
                bool hasAny = paymentsForBill.Any();

                bill.Status = hasVerified ? "Paid" : hasAny ? "Pending" : "Unpaid";
            }

            // Get ALL unpaid bills from the latest 5
            var filteredBills = recentBills
                .Where(b => b.Status == "Unpaid")
                .OrderByDescending(b => b.BillingDate)
                .ToList();

            var recentPayments = await _context.Payments
                .Where(p => p.ConsumerId == consumer.Id)
                .Include(p => p.Billing)
                .OrderByDescending(p => p.PaymentDate)
                .Take(5000)
                .ToListAsync();

            var vm = new UserDashboardViewModel
            {
                Consumer = consumer,
                RecentBills = filteredBills, // ← now shows ALL unpaid
                RecentPayments = recentPayments
            };

            return View(vm);
        }







        ///////////////////////////
        //     BILLING HISTORY   //
        ///////////////////////////


        // ================== SHOW THE BILLING HISTORY VIEW ==================

        // GET: /User/BillingHistory
        [Authorize(Roles = "User")]
        public async Task<IActionResult> BillingHistory(string? searchTerm, string? statusFilter)
        {
            int userId = int.Parse(User.FindFirst("UserId")!.Value);

            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null) return NotFound();

            // Use AccountNumber instead of userId
            await _audit.LogAsync(
                "Viewed Billing History",
                $"User ({consumer.User.AccountNumber}) viewed their billing history. Filters - SearchTerm: '{searchTerm}', Status: '{statusFilter}'",
                consumer.User.AccountNumber
            );


            // Build billing query
            var query = _context.Billings
                .Where(b => b.ConsumerId == consumer.Id)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(b =>
                    b.Status.Contains(searchTerm) ||
                    b.BillingDate.ToString().Contains(searchTerm)
                );
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(b => b.Status == statusFilter);
            }

            var billings = await query.OrderByDescending(b => b.BillingDate).ToListAsync();

            var billingIds = billings.Select(b => b.Id).ToList();

            // One query to get all payment statuses
            var paymentStatuses = await _context.Payments
                .Where(p => billingIds.Contains(p.BillingId))
                .GroupBy(p => p.BillingId)
                .Select(g => new
                {
                    BillingId = g.Key,
                    HasVerified = g.Any(p => p.IsVerified),
                    HasAny = g.Any()
                })
                .ToListAsync();

            // Build view models
            var billingViewModels = billings.Select(b =>
            {
                var statusInfo = paymentStatuses.FirstOrDefault(p => p.BillingId == b.Id);

                string status = statusInfo?.HasVerified == true
                    ? "Paid"
                    : statusInfo?.HasAny == true
                        ? "Pending"
                        : "Unpaid";

                return new BillingViewModel
                {
                    Id = b.Id,
                    BillNo = b.BillNo ?? "",
                    BillingDate = b.BillingDate,
                    DueDate = b.DueDate,
                    PreviousReading = b.PreviousReading,
                    PresentReading = b.PresentReading,
                    CubicMeterUsed = b.CubicMeterUsed,
                    AmountDue = b.AmountDue,
                    Penalty = b.Penalty,
                    AdditionalFees = b.AdditionalFees,
                    TotalAmount = b.TotalAmount,
                    Status = status,
                    FullName = $"{consumer.FirstName} {(string.IsNullOrWhiteSpace(consumer.MiddleName) ? "" : consumer.MiddleName + " ")}{consumer.LastName}".Trim(),
                    AccountNumber = consumer.User?.AccountNumber ?? ""
                };
            }).ToList();

            var viewModel = new BillingHistoryViewModel
            {
                ConsumerName = consumer.FirstName,
                ConsumerAddress = consumer.HomeAddress,
                Billings = billingViewModels
            };

            return View(viewModel);
        }



        // ================== SHOW THE BILLING DETAILS VIEW ==================

        // GET: /User/BillingDetails/{id}
        [Authorize(Roles = "User")]
        public async Task<IActionResult> BillingDetails(int id)
        {
            var userId = int.Parse(User.FindFirst("UserId")!.Value);
            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null) return NotFound();

            var billing = await _context.Billings.FirstOrDefaultAsync(b => b.Id == id && b.ConsumerId == consumer.Id);
            if (billing == null) return NotFound();

            // Log activity with AccountNumber instead of userId
            await _audit.LogAsync(
                "Viewed Billing Details",
                $"User ({consumer.User.AccountNumber}) viewed details for BillNo: {billing.BillNo}, Billing Date: {billing.BillingDate:d}",
                consumer.User.AccountNumber
            );

            var viewModel = new BillingViewModel
            {
                Id = billing.Id,
                BillNo = billing.BillNo ?? "",
                BillingDate = billing.BillingDate,
                DueDate = billing.DueDate,
                AmountDue = billing.AmountDue,
                PreviousReading = billing.PreviousReading,
                PresentReading = billing.PresentReading,
                CubicMeterUsed = billing.CubicMeterUsed,
                Penalty = billing.Penalty,
                AdditionalFees = billing.AdditionalFees ?? 0,
                TotalAmount = billing.TotalAmount,
                Status = billing.Status,
                FullName = $"{consumer.FirstName} {(string.IsNullOrWhiteSpace(consumer.MiddleName) ? "" : consumer.MiddleName + " ")}{consumer.LastName}".Trim(),
                AccountNumber = consumer.User?.AccountNumber ?? ""
            };

            return View(viewModel);
        }



        // ================== DOWNLOAD BILLING HISTORY AS PDF ==================

        // GET: /User/DownloadBillingHistoryPdf
        [Authorize(Roles = "User")]
        [HttpGet]
        public async Task<IActionResult> DownloadBillingHistoryPdf(string? searchTerm, string? statusFilter)
        {
            int userId = int.Parse(User.FindFirst("UserId")!.Value);

            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null)
                return NotFound();

            var billingsQuery = _context.Billings
                .Where(b => b.ConsumerId == consumer.Id);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                billingsQuery = billingsQuery.Where(b =>
                    b.BillNo.Contains(searchTerm) ||
                    b.Status.Contains(searchTerm));
            }

            if (!string.IsNullOrEmpty(statusFilter))
            {
                billingsQuery = billingsQuery.Where(b => b.Status == statusFilter);
            }

            var billings = await billingsQuery
                .OrderByDescending(b => b.BillingDate)
                .ToListAsync();

            // Map Billing model to BillingViewModel
            var billingViewModels = billings.Select(b => new BillingViewModel
            {
                AccountNumber = consumer.User?.AccountNumber ?? "-",
                FullName = consumer.FullName,
                BillNo = b.BillNo,
                BillingDate = b.BillingDate,
                DueDate = b.DueDate,
                PreviousReading = b.PreviousReading,
                PresentReading = b.PresentReading,
                CubicMeterUsed = b.CubicMeterUsed,
                AmountDue = b.AmountDue,
                Penalty = b.Penalty,
                AdditionalFees = b.AdditionalFees,
                TotalAmount = b.TotalAmount,
                Status = b.Status
            }).ToList();

            var model = new BillingHistoryViewModel
            {
                Billings = billingViewModels
            };

            // Audit Log
            await _audit.LogAsync("Downloaded Billing History PDF",
                $"User downloaded their billing history as PDF. Filter - SearchTerm: '{searchTerm}', Status: '{statusFilter}'",
                userId.ToString());

            var pdfBytes = BillingHistoryPdfService.Generate(model, searchTerm, statusFilter);

            return File(pdfBytes, "application/pdf", "BillingHistory.pdf");
        }




        // ================== DOWNLOAD SINGLE BILLING RECEIPT AS PDF ==================

        // GET: /User/DownloadBillingReceiptPdf/{billingId}
        [Authorize(Roles = "User")]
        public async Task<IActionResult> DownloadBillingReceiptPdf(int billingId)
        {
            int userId = int.Parse(User.FindFirst("UserId")!.Value);

            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null || consumer.User == null)
                return NotFound();

            var billing = await _context.Billings
                .Where(b => b.Id == billingId && b.ConsumerId == consumer.Id)
                .Select(b => new BillingViewModel
                {
                    AccountNumber = consumer.User.AccountNumber ?? "-",
                    FullName = consumer.FullName,
                    BillNo = b.BillNo,
                    BillingDate = b.BillingDate,
                    DueDate = b.DueDate,
                    PreviousReading = b.PreviousReading,
                    PresentReading = b.PresentReading,
                    CubicMeterUsed = b.CubicMeterUsed,
                    AmountDue = b.AmountDue,
                    Penalty = b.Penalty,
                    AdditionalFees = b.AdditionalFees,
                    TotalAmount = b.TotalAmount,
                    Status = b.Status
                })
                .FirstOrDefaultAsync();

            if (billing == null)
                return NotFound();

            // Audit Log
            await _audit.LogAsync("Downloaded Billing Receipt PDF",
                $"User downloaded billing receipt PDF for BillNo: {billing.BillNo}, Billing Date: {billing.BillingDate:d}",
                userId.ToString());

            var pdfBytes = BillingPdfService.Generate(billing);

            return File(pdfBytes, "application/pdf", "BillingReceipt.pdf");
        }








        ///////////////////////////
        //     PAYMENT USER      //
        ///////////////////////////



        // ================== SHOW THE PAYMENT VIEW ==================

        // GET: /User/Payment
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Payment()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            var accountNumber = User.Identity?.Name;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            int userId = int.Parse(userIdClaim);

            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null)
                return NotFound("Consumer not found");

            // Get all bills for the consumer
            var allBills = await _context.Billings
                .Where(b => b.ConsumerId == consumer.Id)
                .ToListAsync();

            // Separate unpaid bills
            var unpaidBills = allBills.Where(b => !b.IsPaid).ToList();
            var paidBills = allBills
                .Where(b => b.IsPaid == true) // strict check
                .ToList();


            // Related payments to unpaid bills
            var billIds = unpaidBills.Select(b => b.Id).ToList();
            var relatedPayments = await _context.Payments
                .Where(p => billIds.Contains(p.BillingId))
                .ToListAsync();

            foreach (var bill in unpaidBills)
            {
                bill.HasPendingPayment = relatedPayments.Any(p => p.BillingId == bill.Id && !p.IsVerified);
                bill.TotalAmount = bill.AmountDue + bill.Penalty + bill.AdditionalFees.GetValueOrDefault();
            }

            // All previous payments
            var previousPayments = await _context.Payments
                .Include(p => p.Billing)
                .Where(p => p.ConsumerId == consumer.Id)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var vm = new UserPaymentViewModel
            {
                Consumer = consumer,
                FirstName = consumer.FirstName,
                MiddleName = consumer.MiddleName,
                LastName = consumer.LastName,
                PendingBills = unpaidBills,
                PreviousPayments = previousPayments,
                PaidBillsCount = allBills.Count(b => b.IsPaid),
                UnpaidBillsCount = unpaidBills.Count

            };

            await _audit.LogAsync(
                "Viewed Payment Page",
                $"User viewed their pending bills and payment history. Unpaid bills count: {unpaidBills.Count}, Previous payments: {previousPayments.Count}",
                accountNumber
            );

            return View(vm);
        }



        // ================== SHOW THE PAYMENT FORM FOR A SPECIFIC BILL ==================

        // GET: /User/Pay/{billId}
        [Authorize(Roles = "User")]
        [HttpGet]
        public async Task<IActionResult> Pay(int billId)
        {
            var userIdClaim = User.FindFirst("UserId");
            var accountNumber = User.Identity?.Name;

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var bill = await _context.Billings
                .Include(b => b.Consumer)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(b => b.Id == billId && b.Consumer.UserId == userId);

            if (bill == null)
                return NotFound("Bill not found or unauthorized");

            var gcashSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "GcashQrImagePath");
            var mayaSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "MayaQrImagePath");

            var model = new PayViewModel
            {
                BillingId = bill.Id,
                FirstName = bill.Consumer.FirstName,
                MiddleName = bill.Consumer.MiddleName,
                LastName = bill.Consumer.LastName,
                ConsumerName = $"{bill.Consumer.FirstName} {bill.Consumer.MiddleName} {bill.Consumer.LastName}".Replace("  ", " ").Trim(),
                AccountNumber = bill.Consumer.User?.AccountNumber,
                AmountDue = bill.AmountDue,
                AdditionalFee = bill.AdditionalFees ?? 0m,
                Penalty = bill.Penalty,
                PreviousReading = bill.PreviousReading,
                PresentReading = bill.PresentReading,
                TransactionId = GenerateTransactionId(),
                GcashQrImageUrl = gcashSetting?.Value ?? "/images/gcash-qr.png",
                MayaQrImageUrl = mayaSetting?.Value ?? "/images/maya-qr.png"
            };

            // Audit logging: viewing payment page for specific bill
            await _audit.LogAsync(
                "Viewed Payment Form",
                $"User accessed payment form for Bill #{bill.BillNo} (ID: {bill.Id})",
                 accountNumber
            );

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(PayViewModel model)
        {
            var userIdClaim = User.FindFirst("UserId");
            var accountNumber = User.Identity?.Name;
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var bill = await _context.Billings
                .Include(b => b.Consumer)
                .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(b => b.Id == model.BillingId && b.Consumer.UserId == userId);

            if (bill == null)
                return NotFound("Bill not found or unauthorized");

            var gcashSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "GcashQrImagePath");
            var mayaSetting = await _context.Settings.FirstOrDefaultAsync(s => s.Key == "MayaQrImagePath");

            model.GcashQrImageUrl = gcashSetting?.Value ?? "/images/gcash-qr.png";
            model.MayaQrImageUrl = mayaSetting?.Value ?? "/images/maya-qr.png";
            model.AccountNumber = bill.Consumer.User?.AccountNumber;

            if (!ModelState.IsValid)
                return View(model);

            if (model.Receipt == null || model.Receipt.Length == 0)
            {
                ModelState.AddModelError("Receipt", "Please upload the receipt image.");
                return View(model);
            }

            if (model.Receipt.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("Receipt", "File size cannot exceed 5 MB.");
                return View(model);
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extension = Path.GetExtension(model.Receipt.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("Receipt", "Invalid file type. Allowed: JPG, JPEG, PNG, PDF.");
                return View(model);
            }

            var supportedTypes = new[] { "image/png", "image/jpeg", "image/jpg", "application/pdf" };
            if (!supportedTypes.Contains(model.Receipt.ContentType.ToLower()))
            {
                ModelState.AddModelError("Receipt", "Invalid file type. Only PNG, JPEG, or PDF allowed.");
                return View(model);
            }

            // Save uploaded receipt file
            string receiptFileName = $"{Guid.NewGuid()}{extension}";
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, receiptFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.Receipt.CopyToAsync(stream);
            }

            // Save payment record
            var payment = new Payment
            {
                ConsumerId = bill.ConsumerId,
                BillingId = bill.Id,
                AmountPaid = model.TotalAmount,
                Method = model.SelectedPaymentMethod,
                PaymentDate = DateTime.Now,
                TransactionId = model.TransactionId,
                ReceiptPath = receiptFileName,
                IsVerified = false
            };

            _context.Payments.Add(payment);

            // Update billing status
            bill.Status = "Pending";
            bill.IsPaid = false;

            await _context.SaveChangesAsync();

            // Audit Logging
            await _audit.LogAsync(
           "Payment Submitted",
           $"User submitted a payment for Bill #{bill.BillNo} using {model.SelectedPaymentMethod}. Amount: ₱{model.TotalAmount:F2}. Transaction ID: {model.TransactionId}",
           accountNumber
            );

            // Add app + push notification for user
            var consumer = bill.Consumer;
            var user = bill.Consumer?.User;

            // In-App Notification
            var paymentNotif = new Notification
            {
                ConsumerId = consumer.Id,
                Title = "💵 Payment Submitted",
                Message = $"Thank you {consumer.FirstName}, your payment of ₱{payment.AmountPaid:N2} for Bill No: {bill.BillNo} has been submitted and is pending verification.",
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(paymentNotif);

            //Push Notification
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
                    title = "💵 Payment Submitted",
                    body = $"₱{payment.AmountPaid:N2} for Bill #{bill.BillNo} submitted successfully."
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

            // Save new notification
            await _context.SaveChangesAsync();

            // ================= EMAIL CONFIRMATION =================
            if (!string.IsNullOrWhiteSpace(consumer.Email))
            {
                var emailSubject = $"Payment Submitted for Bill No: {bill.BillNo}";
                var emailBody = $@"
        <p>Hello <strong>{consumer.FullName}</strong>,</p>
        <p>We have received your payment submission for your water bill.</p>
        <p>
            <strong>Bill No:</strong> {bill.BillNo}<br/>
            <strong>Amount Paid:</strong> ₱{payment.AmountPaid:N2}<br/>
            <strong>Method:</strong> {payment.Method}<br/>
            <strong>Transaction ID:</strong> {payment.TransactionId}<br/>
            <strong>Date Submitted:</strong> {payment.PaymentDate:MMMM d, yyyy hh:mm tt}
        </p>
        <p>Your payment is currently <strong>pending verification</strong> by our admin team. You will receive another email once it has been confirmed.</p>
        <p>Thank you for your payment,<br/>Santa Fe Water System</p>";

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

                await _context.SaveChangesAsync();
            }


            // Store Transaction Info in TempData for Confirmation View
            TempData["TransactionId"] = model.TransactionId;
            TempData["AmountPaid"] = model.TotalAmount.ToString("F2"); // <- 🔧 Fixed here
            TempData["Method"] = model.SelectedPaymentMethod;
            TempData["PaymentDate"] = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");

            // Message for optional alert (you may keep this or remove)
            TempData["Message"] = "Payment submitted successfully. Waiting for admin verification.";

            // Redirect to confirmation view
            return RedirectToAction("PaymentConfirmation");
        }


        // ================== SHOW PAYMENT CONFIRMATION ==================

        // GET: /User/PaymentConfirmation
        public IActionResult PaymentConfirmation()
        {
            // Redirect if accessed directly without payment submission
            if (TempData["TransactionId"] == null ||
                TempData["AmountPaid"] == null ||
                TempData["Method"] == null ||
                TempData["PaymentDate"] == null)
            {
                return RedirectToAction("Dashboard", "User");
            }

            // Keep TempData values alive for the view
            TempData.Keep();

            ViewBag.TransactionId = TempData["TransactionId"];
            ViewBag.AmountPaid = TempData["AmountPaid"];
            ViewBag.Method = TempData["Method"];
            ViewBag.Date = TempData["PaymentDate"];

            return View();
        }



        // ================== HELPER METHODS ==================

        // Generate a unique transaction ID
        private string GenerateTransactionId()
        {
            return "TXN" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(1000, 9999);
        }




        // ================== SHOW PAYMENT RECEIPT ==================

        // GET: /User/PaymentReceipt/{paymentId}
        [Authorize(Roles = "User")]
        [HttpGet]
        public async Task<IActionResult> PaymentReceipt(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Billing)
                .Include(p => p.Consumer)
                    .ThenInclude(c => c.User) //Load User from Consumer
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
                return NotFound();

            // Safely build Full Name if User.FullName is null
            var consumer = payment.Consumer;
            var user = consumer?.User;

            var fullName = user?.FullName;
            if (string.IsNullOrWhiteSpace(fullName))
            {
                var first = consumer?.FirstName ?? "";
                var middle = string.IsNullOrWhiteSpace(consumer?.MiddleName) ? "" : consumer.MiddleName + " ";
                var last = consumer?.LastName ?? "";
                fullName = $"{first} {middle}{last}".Trim();
            }

            var viewModel = new PaymentReceiptViewModel
            {
                PaymentId = payment.Id,
                AccountNumber = user?.AccountNumber,
                FullName = fullName,
                BillNo = payment.Billing?.BillNo,
                PaymentDate = payment.PaymentDate,
                AmountPaid = payment.AmountPaid,
                Method = payment.Method,
                TransactionId = payment.TransactionId,
                Status = payment.IsVerified ? "Verified" : "Pending"
            };

            return View(viewModel);
        }







        ///////////////////////////
        //     PROFILE USER      //
        ///////////////////////////


        // ================== SHOW THE PROFILE VIEW ==================

        // GET: /User/Profile
        [Authorize(Roles = "User")]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null) return NotFound();

            var vm = new UserProfileViewModel
            {
                Id = consumer.Id,
                FirstName = consumer.FirstName,
                MiddleName = consumer.MiddleName,
                LastName = consumer.LastName,
                Address = consumer.HomeAddress,
                ContactNumber = consumer.ContactNumber,
                Email = consumer.Email,
                AccountNumber = consumer.User?.AccountNumber,
                AccountType = consumer.AccountType,
                MeterNo = consumer.MeterNo,
                ExistingProfilePicture = consumer.ProfilePicture
            };
            // Log audit for viewing profile
            await _audit.LogAsync("Viewed Profile", "User viewed their profile page.", consumer.User?.AccountNumber ?? userId.ToString());

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var entry in ModelState)
                {
                    foreach (var error in entry.Value.Errors)
                    {
                        Console.WriteLine($"Validation error on {entry.Key}: {error.ErrorMessage}");
                    }
                }

                TempData["Error"] = "Update failed. Please correct the highlighted errors.";
                return View(model);
            }

            var consumer = await _context.Consumers.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == model.Id);
            if (consumer == null)
                return NotFound();

            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId) || consumer.UserId != userId)
                return Unauthorized();

            // Track old values before updating
            var originalEmail = consumer.Email;
            var originalContact = consumer.ContactNumber;
            var oldPicture = consumer.ProfilePicture;


            // Only update editable fields
            consumer.ContactNumber = model.ContactNumber;
            consumer.Email = model.Email;

            // Handle profile picture upload
            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "profiles");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProfileImage.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfileImage.CopyToAsync(stream);
                }

                // Optional: delete old profile picture
                if (!string.IsNullOrEmpty(consumer.ProfilePicture))
                {
                    var oldPath = Path.Combine(uploadsFolder, consumer.ProfilePicture);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                consumer.ProfilePicture = uniqueFileName;
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = "Profile updated successfully.";

            // Log audit for profile update
            await _audit.LogAsync(
                "Updated Profile",
                $"User updated profile info. Email: {originalEmail} → {consumer.Email}, Contact: {originalContact} → {consumer.ContactNumber}",
                consumer.User?.AccountNumber ?? userId.ToString()
            );

            return RedirectToAction("Profile");
        }










        ///////////////////////////
        //     SUPPORT  USER     //
        ///////////////////////////



        // ================== SHOW THE SUPPORT VIEW ==================

        // GET: /User/Support
        [Authorize(Roles = "User")]
        [HttpGet]
        public async Task<IActionResult> Support()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (consumer == null)
            {
                TempData["ToastType"] = "danger";
                TempData["ToastMessage"] = "Consumer account not found.";
                return RedirectToAction("Index", "Home");
            }

            var tickets = await _context.Supports
                .Where(s => s.ConsumerId == consumer.Id)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var vm = new UserSupportViewModel
            {
                PreviousTickets = tickets
            };

            return View(vm);
        }

        [Authorize(Roles = "User")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Support(UserSupportViewModel model)
        {
            var userIdClaim = User.FindFirst("UserId");
            var accountNumber = User.Identity?.Name; 

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (consumer == null)
            {
                TempData["ToastType"] = "danger";
                TempData["ToastMessage"] = "Unable to find your account. Please contact support.";
                return RedirectToAction("Support");
            }

            if (ModelState.IsValid)
            {
                var support = new Support
                {
                    ConsumerId = consumer.Id,
                    Subject = model.Subject,
                    Message = model.Message,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Open",              //  Ensure it is marked Open
                    IsResolved = false,           //  Not resolved by default
                    AdminReply = null,            //  No reply yet
                    IsReplySeen = false,
                    IsArchived = false
                };

                _context.Supports.Add(support);
                await _context.SaveChangesAsync();

                // Use accountNumber in audit log
                await _audit.LogAsync(
                    "Submitted Support Ticket",
                    $"Subject: {support.Subject}. Message: {support.Message}",
                    accountNumber
                );

                TempData["ToastType"] = "success";
                TempData["ToastMessage"] = "Support request submitted successfully!";
                return RedirectToAction("Support");
            }

            TempData["ToastType"] = "danger";
            TempData["ToastMessage"] = "There were issues with your submission. Please correct the form.";

            // Repopulate previous tickets
            model.PreviousTickets = await _context.Supports
                .Where(s => s.ConsumerId == consumer.Id)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(model);
        }


        // ================== DELETE A SUPPORT TICKET ==================

        // DELETE: /User/DeleteSupport/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSupport(int id)
        {
            var support = await _context.Supports.FindAsync(id);
            if (support == null)
                return NotFound();

            _context.Supports.Remove(support);
            await _context.SaveChangesAsync();

            return Ok();
        }








        ///////////////////////////
        //    FEEDBACK USER      //
        ///////////////////////////




        // ================== SHOW THE FEEDBACK VIEW ==================

        // Show the empty feedback form
        [HttpGet]
        public IActionResult Feedback()
        {
            return View("~/Views/User/Feedback.cshtml", new Feedback());
        }

        // Process submitted feedback form
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Feedback(Feedback model)
        {
            if (ModelState.IsValid)
            {
                var userIdString = User.FindFirstValue("UserId");

                if (int.TryParse(userIdString, out int userId))
                {
                    var consumer = await _context.Consumers.Include(c => c.User)
                                                           .FirstOrDefaultAsync(c => c.UserId == userId);

                    var accountNumber = consumer?.User?.AccountNumber ?? "Unknown";

                    model.UserId = userId;
                    model.SubmittedAt = DateTime.UtcNow;

                    _context.Feedbacks.Add(model);
                    await _context.SaveChangesAsync();

                    //  Use AccountNumber instead of userId
                    await _audit.LogAsync(
                        "Submitted Feedback",
                        $"User submitted feedback with rating {model.Rating}. Comment: {model.Comment}",
                        accountNumber // correct value
                    );

                    TempData["Message"] = "Thank you for your feedback!";
                    return RedirectToAction("Feedback"); // reload form with success message
                }
                ModelState.AddModelError("", "Unable to determine your user ID.");
            }

            return View("~/Views/User/Feedback.cshtml", model);
        }









        ///////////////////////////
        //    RESET PASS  USER    //
        ///////////////////////////





        // ================== SHOW THE RESET PASSWORD VIEW ==================
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ResetPassword()
        {
            // 🔹 Always get the latest password policy from DB
            var policy = await _context.PasswordPolicies
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (policy == null)
            {
                // Fallback defaults
                policy = new PasswordPolicy
                {
                    MinPasswordLength = 8,
                    RequireComplexity = true,
                    PasswordHistoryCount = 5,
                    MaxPasswordAgeDays = 0,
                    MinPasswordAgeDays = 1
                };
            }

            // Pass policy to view
            ViewBag.PasswordPolicy = policy;

            return View(new ResetPasswordViewModel());
        }

        // ================== PROCESS PASSWORD RESET ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User")]
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

            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user == null)
                return NotFound();

            // 🔹 Verify current password safely
            bool isCurrentPasswordValid = false;
            try
            {
                isCurrentPasswordValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // Log warning if needed
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
                TempData["Error"] = "❌ Change password failed — your new password does not meet the requirements.";
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
                Details = $"User '{user.AccountNumber}' successfully reset their password."
            });

            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ Password changed successfully.";
            return RedirectToAction("Index", " ConsumerSetting");
        }




    }
}
