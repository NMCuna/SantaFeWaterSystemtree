using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Extensions;
using SantaFeWaterSystem.Helpers;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Models.ViewModels;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.Settings;
using System.Security.Claims;
using System.Text.Json;
using X.PagedList;
using X.PagedList.Extensions;
using SantaFeWaterSystem.Filters;


namespace SantaFeWaterSystem.Controllers
{
    [Authorize(Roles = "Admin,Staff,User")]
    public class NotificationsController(ApplicationDbContext context, ISmsQueue smsQueue, ISemaphoreSmsService smsService, PermissionService permissionService,
         IWebHostEnvironment env, IOptions<SemaphoreSettings> semaphoreOptions, AuditLogService audit) : BaseController(permissionService, context, audit)
    {
        private const int PageSize = 5;
        private readonly ISmsQueue _smsQueue = smsQueue;
        private readonly ISemaphoreSmsService _smsService = smsService;        
        private readonly IWebHostEnvironment _env = env;
        private readonly SemaphoreSettings _semaphoreSettings = semaphoreOptions.Value;



        //================== INDEX LIST OF NOTIFICATIONS ==================

        // GET: Notifications List with search and pagination
        [Authorize(Roles = "Admin,Staff")]
        public IActionResult Index(string search, int pageNumber = 1)
        {
            int pageSize = 7;
            var query = _context.Notifications
                .Include(n => n.Consumer)
                .Where(n => !n.IsArchived);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(n => n.Title.Contains(search) || n.Message.Contains(search));

            var ordered = query.OrderByDescending(n => n.CreatedAt);
            var total = ordered.Count();
            var items = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var paged = new StaticPagedList<Notification>(items, pageNumber, pageSize, total);
            return View(paged);
        }



        //================== MARK AS READ ==================

        // Mark as read via AJAX
        [Authorize(Roles = "User,Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] int id)
        {
            var notif = await _context.Notifications.FindAsync(id);
            if (notif == null)
                return NotFound();

            if (!notif.IsRead)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }



        //================== DELETE NOTIFICATION IN ADMIN ACTION==================

        // Delete via AJAX
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteViaAjax([FromBody] int id)
        {
            var notif = await _context.Notifications.FindAsync(id);
            if (notif == null) return NotFound();

            _context.Notifications.Remove(notif);
            await _context.SaveChangesAsync();
            return Ok();
        }



        //================== CREATE NOTIFICATION ==================

        // GET: Notification/Create
        [Authorize(Roles = "Admin,Staff")]
        // GET: Notification/Create
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Notification notification)
        {
            if (!ModelState.IsValid)
                return View(notification);

            notification.CreatedAt = DateTime.Now;

            // For broadcast: notify all consumers with linked users
            if (notification.SendToAll)
            {
                var consumers = await _context.Consumers
                    .Include(c => c.User)
                    .Where(c => c.User != null)
                    .ToListAsync();

                foreach (var consumer in consumers)
                {
                    // Add in-app notification
                    var userNotification = new Notification
                    {
                        Title = notification.Title,
                        Message = notification.Message,
                        ConsumerId = consumer.Id,
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        IsArchived = false,
                        SendToAll = true
                    };
                    _context.Notifications.Add(userNotification);

                    // Push Notification
                    var subscriptions = await _context.UserPushSubscriptions
                        .Where(s => s.UserId == consumer.UserId)
                        .ToListAsync();

                    if (subscriptions.Any())
                    {
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
                            title = notification.Title,
                            body = notification.Message
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
                                Console.WriteLine($"[Push Error - Broadcast] {ex.Message}");
                            }
                        }
                    }
                }
            }
            else
            {
                // Send to specific consumer only
                _context.Notifications.Add(notification);

                var consumer = await _context.Consumers
                    .FirstOrDefaultAsync(c => c.Id == notification.ConsumerId && c.UserId != null);

                if (consumer != null)
                {
                    var subscriptions = await _context.UserPushSubscriptions
                        .Where(s => s.UserId == consumer.UserId)
                        .ToListAsync();

                    if (subscriptions.Any())
                    {
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
                            title = notification.Title,
                            body = notification.Message
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
                                Console.WriteLine($"[Push Error - Individual] {ex.Message}");
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Notification sent successfully.";
            return RedirectToAction("Index", "Notifications");
        }




        //================== DELETE NOTIFICATION IN ADMIN ==================

        // GET: Notification/Delete/5
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
                return NotFound();

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }



        //================== ARCHIVE NOTIFICATION ==================

        // Archive single notification
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> Archive(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
                return NotFound();

            notification.IsArchived = true;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }



        //================== UNARCHIVE NOTIFICATION ==================

        // Unarchive single notification
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unarchive(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null)
            {
                return NotFound();
            }

            notification.IsArchived = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Notification successfully unarchived.";
            return RedirectToAction("Archived"); // redirect back to archived view
        }



        //================== VIEW ARCHIVED NOTIFICATIONS ==================

        // View Archived Notifications with pagination
        [Authorize(Roles = "Admin,Staff")]
        public IActionResult Archived(int page = 1, int pageSize = 8)
        {
            var totalArchived = _context.Notifications.Count(n => n.IsArchived);

            var archived = _context.Notifications
                .Include(n => n.Consumer)
                .Where(n => n.IsArchived)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalArchived / pageSize);

            return View("Archived", archived);
        }



        //================== ARCHIVE SELECTED NOTIFICATIONS ==================

        // Archive selected notifications
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArchiveSelectedByAdmin(string selectedIds)
        {
            if (string.IsNullOrEmpty(selectedIds))
                return RedirectToAction("Index");

            var ids = selectedIds.Split(',').Select(int.Parse).ToList();

            var notifications = await _context.Notifications
                .Where(n => ids.Contains(n.Id))
                .ToListAsync();

            foreach (var n in notifications)
            {
                n.IsArchived = true;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{notifications.Count} notifications archived.";
            return RedirectToAction("Index");
        }






        //================== SEND SMS CONTROLLER ==================

        // GET: SendSms
        [Authorize(Roles = "Admin,Staff")]
        [HttpGet]
        public IActionResult SendSms(string searchTerm, int page = 1)
        {
            var consumersQuery = _context.Billings
                .Where(b => !b.IsPaid && b.Consumer != null)
                .Select(b => b.ConsumerId)
                .Distinct()
                .Join(_context.Consumers.Include(c => c.User), id => id, c => c.Id, (id, c) => c);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                consumersQuery = consumersQuery.Where(c =>
                    c.FirstName.Contains(searchTerm) ||
                    c.LastName.Contains(searchTerm));
            }

            var totalConsumers = consumersQuery.Count();
            var consumersWithBills = consumersQuery
                .OrderBy(c => c.FirstName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            foreach (var c in consumersWithBills)
            {
                c.Billings = _context.Billings.Where(b => b.ConsumerId == c.Id && !b.IsPaid).ToList();
            }

            var viewModel = new SmsNotificationViewModel
            {
                SearchTerm = searchTerm,
                PageNumber = page,
                TotalPages = (int)Math.Ceiling(totalConsumers / (double)PageSize),
                ConsumersWithBills = consumersWithBills
            };

            return View(viewModel);
        }


        // POST: SendSms
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSms(SmsNotificationViewModel model)
        {
            List<Consumer> recipients;

            if (model.SendToAll)
            {
                recipients = _context.Billings
                    .Where(b => !b.IsPaid && b.Consumer != null)
                    .Select(b => b.ConsumerId)
                    .Distinct()
                    .Join(_context.Consumers.Include(c => c.User), id => id, c => c.Id, (id, c) => c)
                    .ToList();

                foreach (var c in recipients)
                {
                    c.Billings = _context.Billings.Where(b => b.ConsumerId == c.Id && !b.IsPaid).ToList();
                }
            }
            else
            {
                if (model.SelectedConsumerIds == null || !model.SelectedConsumerIds.Any())
                {
                    ModelState.AddModelError("", "Please select at least one consumer.");
                    ReloadModel(model);
                    return View(model);
                }

                recipients = _context.Consumers
                    .Where(c => model.SelectedConsumerIds.Contains(c.Id))
                    .Include(c => c.User)
                    .ToList();

                foreach (var c in recipients)
                {
                    c.Billings = _context.Billings.Where(b => b.ConsumerId == c.Id && !b.IsPaid).ToList();
                }
            }

            if (!recipients.Any())
            {
                ModelState.AddModelError("", "No consumers found to send SMS.");
                ReloadModel(model);
                return View(model);
            }

            foreach (var consumer in recipients)
            {
                if (!string.IsNullOrWhiteSpace(consumer.ContactNumber))
                {
                    var billing = consumer.Billings?.FirstOrDefault();
                    var amount = billing?.AmountDue.ToString("N2") ?? "0.00";
                    var dueDate = billing?.DueDate.ToString("MMMM dd") ?? "N/A";
                    var account = consumer.User?.AccountNumber ?? "N/A";

                    var personalizedMessage = model.Message
                        .Replace("{Name}", consumer.FirstName)
                        .Replace("{Amount}", amount)
                        .Replace("{DueDate}", dueDate)
                        .Replace("{AccountNumber}", account);

                    // Send using mock or real service
                    if (_env.IsDevelopment())
                    {
                        await _smsService.SendSmsAsync(consumer.ContactNumber, personalizedMessage);
                    }
                    else
                    {
                        await _smsService.SendSmsAsync(consumer.ContactNumber, personalizedMessage);
                    }

                    _context.Notifications.Add(new Notification
                    {
                        ConsumerId = consumer.Id,
                        Title = "Water Bill Reminder",
                        Message = personalizedMessage,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = $"SMS sent to {recipients.Count} consumer(s).";
            return RedirectToAction("SendSms", new { searchTerm = model.SearchTerm });
        }

        private void ReloadModel(SmsNotificationViewModel model)
        {
            var consumersQuery = _context.Billings
                .Where(b => !b.IsPaid && b.Consumer != null)
                .Select(b => b.ConsumerId)
                .Distinct()
                .Join(_context.Consumers.Include(c => c.User), id => id, c => c.Id, (id, c) => c);

            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
            {
                consumersQuery = consumersQuery.Where(c =>
                    c.FirstName.Contains(model.SearchTerm) || c.LastName.Contains(model.SearchTerm));
            }

            var totalConsumers = consumersQuery.Count();
            var consumersWithBills = consumersQuery
                .OrderBy(c => c.FirstName)
                .Skip((model.PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            foreach (var c in consumersWithBills)
            {
                c.Billings = _context.Billings.Where(b => b.ConsumerId == c.Id && !b.IsPaid).ToList();
            }

            model.ConsumersWithBills = consumersWithBills;
            model.TotalPages = (int)Math.Ceiling(totalConsumers / (double)PageSize);
        }




        //================== SMS LOGS CONTROLLER LIST OF SMS SENT ==================

        // GET: SmsLogs
        [Authorize(Roles = "Admin,Staff")]
        // View Active SMS Logs
        public async Task<IActionResult> SmsLogs(int? page)
        {
            int pageSize = 9;
            int pageNumber = page ?? 1;

            var logs = await _context.SmsLogs
                .Include(l => l.Consumer)
                .Where(l => !l.IsArchived)
                .OrderByDescending(l => l.SentAt)
                .Select(l => new SmsLogViewModel
                {
                    Id = l.Id,
                    ConsumerName = l.Consumer.FullName,
                    ContactNumber = l.ContactNumber,
                    Message = l.Message,
                    SentAt = l.SentAt,
                    IsSuccess = l.IsSuccess,
                    ResponseMessage = l.ResponseMessage,
                    IsArchived = l.IsArchived
                })
                .ToPagedListAsync(pageNumber, pageSize);

            return View(logs);
        }



        //================== ARCHIVED SMS LOGS CONTROLLER LIST OG ARCHIVE SMS SENT ==================

        // View Archived Logs with filtering
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> ArchivedSmsLogs(int? month, int? year, int? page)
        {
            int selectedMonth = month ?? DateTime.Now.Month;
            int selectedYear = year ?? DateTime.Now.Year;
            int pageSize = 8;
            int pageNumber = page ?? 1;

            var query = _context.SmsLogs
                .Include(l => l.Consumer)
                .Where(l =>
                    l.IsArchived &&
                    l.SentAt.Month == selectedMonth &&
                    l.SentAt.Year == selectedYear)
                .OrderByDescending(l => l.SentAt)
                .Select(l => new SmsLogViewModel
                {
                    Id = l.Id,
                    ContactNumber = l.ContactNumber,
                    Message = l.Message,
                    SentAt = l.SentAt,
                    IsSuccess = l.IsSuccess,
                    ResponseMessage = l.ResponseMessage,
                    ConsumerName = l.Consumer.FullName,
                    IsArchived = l.IsArchived
                });

            ViewBag.SelectedMonth = selectedMonth;
            ViewBag.SelectedYear = selectedYear;

            return View(await query.ToPagedListAsync(pageNumber, pageSize));
        }



        //================== ARCHIVE SELECTED SMS LOGS CONTROLLER ==================
        // Archive selected logs
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> ArchiveSelected(List<int> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "No SMS logs selected.";
                return RedirectToAction("SmsLogs");
            }

            var logsToArchive = await _context.SmsLogs
                .Where(s => selectedIds.Contains(s.Id) && !s.IsArchived)
                .ToListAsync();

            foreach (var log in logsToArchive)
            {
                log.IsArchived = true;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"{logsToArchive.Count} SMS logs archived.";
            return RedirectToAction("SmsLogs");
        }




        //================== UNARCHIVE SMS LOGS CONTROLLER ==================

        // Unarchive single log
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> UnarchiveSmsLog(int id)
        {
            var log = await _context.SmsLogs.FindAsync(id);
            if (log == null)
                return NotFound();

            log.IsArchived = false;
            await _context.SaveChangesAsync();

            return RedirectToAction("ArchivedSmsLogs");
        }





        //================== USER NOTIFICATIONS CONTROLLER LIST OF NOTIF IN USER  ==================

        // View user notifications with filtering
        [Authorize(Roles = "User")]
        [RequirePrivacyAgreement]
        public async Task<IActionResult> UserNotification(string filter = "all")
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (consumer == null)
                return Content("Consumer not found.");

            var query = _context.Notifications
                .Where(n => n.ConsumerId == consumer.Id);

            if (filter == "unread")
                query = query.Where(n => !n.IsRead);
            else if (filter == "read")
                query = query.Where(n => n.IsRead);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            ViewBag.Filter = filter;
            return View("UserNotification", notifications);
        }



        //================== USER NOTIFICATIONS CONTROLLER MARK ALL AS READ IN USER  ==================

        // Mark all as read
        [Authorize(Roles = "User")]
        [RequirePrivacyAgreement]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (consumer == null) return NotFound();

            var notifications = await _context.Notifications
                .Where(n => (n.ConsumerId == consumer.Id || n.SendToAll) && !n.IsRead)
                .ToListAsync();

            foreach (var notif in notifications)
                notif.IsRead = true;

            await _context.SaveChangesAsync();
            return RedirectToAction("UserNotification");
        }



        //================== USER NOTIFICATIONS CONTROLLER DELETE NOTIF IN USER  ==================

        // Delete single notification
        [Authorize(Roles = "User")]
        [RequirePrivacyAgreement]
        [HttpPost] // <-- use POST instead
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (consumer == null) return NotFound();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && (n.ConsumerId == consumer.Id || n.SendToAll));

            if (notification == null) return NotFound();

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(); // return 200 OK
        }



        //================== USER NOTIFICATIONS CONTROLLER GET UNREAD COUNT IN USER  ==================

        // Get unread count for badge
        [Authorize(Roles = "User")]
        [RequirePrivacyAgreement]
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Json(0);

            var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (consumer == null) return Json(0);

            var count = await _context.Notifications
               .CountAsync(n => n.ConsumerId == consumer.Id && !n.IsRead);


            return Json(count);
        }



        //================== USER NOTIFICATIONS CONTROLLER MARK AS READ VIA AJAX IN USER  ==================

        // Mark single notification as read via AJAX    
        [Authorize(Roles = "User")]
        [RequirePrivacyAgreement]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var consumer = await _context.Consumers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (consumer == null) return NotFound();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && (n.ConsumerId == consumer.Id || n.SendToAll));

            if (notification == null) return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}

