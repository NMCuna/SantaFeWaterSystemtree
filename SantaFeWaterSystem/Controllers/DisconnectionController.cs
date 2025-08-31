using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.ViewModels;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SantaFeWaterSystem.Controllers
{
    public class DisconnectionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 9;
        private readonly AuditLogService _audit;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DisconnectionController(ApplicationDbContext context, AuditLogService audit, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _audit = audit;
            _httpContextAccessor = httpContextAccessor;
        }



        //================== HELPER METHODS ==================

        // Helper to get current username
        private string GetCurrentUsername()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Unknown";
        }




        //================== INDEX DISCONNECTION LIST  ==================
        public async Task<IActionResult> Index(string searchTerm, string sortOrder, int page = 1, int pageSize = PageSize)
        {
            var today = DateTime.Today;

            // Step 1: Get overdue billing info grouped by ConsumerId (materialized in memory)
            var overdueGrouped = await _context.Billings
                .Where(b => !b.IsPaid && b.DueDate < today)
                .GroupBy(b => b.ConsumerId)
                .Where(g => g.Count() >= 2)
                .Select(g => new
                {
                    ConsumerId = g.Key,
                    OverdueBillsCount = g.Count(),
                    TotalUnpaidAmount = g.Sum(b => b.TotalAmount),
                    LatestDueDate = g.Max(b => b.DueDate)
                })
                .ToListAsync(); // Now in memory

            // Step 2 & 3: Switch Consumers query to in-memory before join
            var disconnectionData = _context.Consumers
                .AsEnumerable() // Forces client-side join
                .Join(overdueGrouped,
                    c => c.Id,
                    b => b.ConsumerId,
                    (c, b) => new DisconnectionViewModel
                    {
                        ConsumerId = c.Id,
                        ConsumerName = c.FullName,
                        OverdueBillsCount = b.OverdueBillsCount,
                        TotalUnpaidAmount = b.TotalUnpaidAmount,
                        LatestDueDate = b.LatestDueDate,
                        IsDisconnected = c.IsDisconnected
                    })
                .AsQueryable();

            // Step 4: Search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                disconnectionData = disconnectionData
                    .Where(d => d.ConsumerName.ToLower().Contains(searchTerm) ||
                                d.ConsumerId.ToString().Contains(searchTerm));
            }

            // Step 5: Sorting
            disconnectionData = sortOrder switch
            {
                "name_desc" => disconnectionData.OrderByDescending(d => d.ConsumerName),
                "overdue" => disconnectionData.OrderBy(d => d.OverdueBillsCount),
                "overdue_desc" => disconnectionData.OrderByDescending(d => d.OverdueBillsCount),
                "amount" => disconnectionData.OrderBy(d => d.TotalUnpaidAmount),
                "amount_desc" => disconnectionData.OrderByDescending(d => d.TotalUnpaidAmount),
                "duedate" => disconnectionData.OrderBy(d => d.LatestDueDate),
                "duedate_desc" => disconnectionData.OrderByDescending(d => d.LatestDueDate),
                _ => disconnectionData.OrderBy(d => d.ConsumerName)
            };

            // Step 6: Pagination
            var count = disconnectionData.Count();
            var items = disconnectionData
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var paginated = new PaginatedList<DisconnectionViewModel>(items, count, page, pageSize);

            // Pass values to view for search + sorting persistence
            ViewBag.CurrentSort = sortOrder;
            ViewBag.SearchTerm = searchTerm;

            return View(paginated);
        }



        //================== DETAILS  DISCONNECTION  ==================

        // GET: Disconnection/Details
        public async Task<IActionResult> Details(int id)
        {
            var consumer = await _context.Consumers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consumer == null)
                return NotFound();

            var overdueBills = await _context.Billings
                .Where(b => b.ConsumerId == id && !b.IsPaid && b.DueDate < DateTime.Today)
                .ToListAsync();

            var latestDueDate = overdueBills
                .OrderByDescending(b => b.DueDate)
                .FirstOrDefault()?.DueDate;

            var totalUnpaidAmount = overdueBills.Sum(b => b.TotalAmount);

            var disconnection = await _context.Disconnections
                .Where(d => d.ConsumerId == id)
                .OrderByDescending(d => d.DateDisconnected)
                .FirstOrDefaultAsync();

            var viewModel = new DisconnectionViewModel
            {
                ConsumerId = consumer.Id,
                ConsumerName = $"{consumer.FirstName} {consumer.LastName}",
                OverdueBillsCount = overdueBills.Count,
                TotalUnpaidAmount = totalUnpaidAmount,
                LatestDueDate = latestDueDate,
                IsDisconnected = disconnection != null
            };

            return View(viewModel); // now matches your view model structure
        }




        //================== DISCONNECT ACTIONS ==================

        // POST: Disconnection/Disconnect
        [Authorize(Roles = "Admin,Staff")]
        [HttpPost]
        public async Task<IActionResult> Disconnect(int id)
        {
            var consumer = await _context.Consumers
                .Include(c => c.Billings)
                .Include(c => c.User) // Include User for push notification
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consumer == null)
                return NotFound();

            consumer.IsDisconnected = true;
            consumer.Status = "Disconnected";

            var disconnection = new Disconnection
            {
                ConsumerId = id,
                DateDisconnected = DateTime.Now,
                Remarks = "2 or more overdue bills",
                IsReconnected = false,
                Action = "Disconnected",
                PerformedBy = GetCurrentUsername() ?? "Unknown"
            };

            _context.Disconnections.Add(disconnection);

            // Store current date in a readable format
            var notificationDate = DateTime.Now.ToString("MMMM dd, yyyy");

            // In-App Notification
            var notif = new Notification
            {
                ConsumerId = consumer.Id,
                Title = "🛑 Water Service Disconnected",
                Message = $"Hello {consumer.FirstName}, you failed to pay any bill from your 2 overdue bills as of {notificationDate}. " +
                          $"Your water service has been disconnected. To reconnect, please visit the main office of Santa Fe Water System located at the Santa Fe Municipal Hall. Thank you.",
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notif);

            // Push Notification
            var user = consumer.User;
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
                    title = "\U0001f6d1 Water Service Disconnected",
                    body = $"Your water service has been disconnected on {notificationDate} due to 2 or more overdue bills. Please visit the office to reconnect."
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

            // Audit log
            var audit = new AuditTrail
            {
                Action = "Disconnect",
                PerformedBy = GetCurrentUsername(),
                Details = $"Disconnected Consumer ID {id} due to 2 or more overdue bills.",
                Timestamp = DateTime.Now
            };
            _context.AuditTrails.Add(audit);

            await _context.SaveChangesAsync();

            TempData["Message"] = $"Consumer {consumer.FirstName} has been disconnected and notified.";

            return RedirectToAction(nameof(Index));
        }




        //================== RECONNECT ACTIONS ==================

        // POST: Disconnection/Reconnect
        [HttpPost]
        public async Task<IActionResult> Reconnect(int id)
        {
            var consumer = await _context.Consumers
                .Include(c => c.User) // Include User for push notification
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consumer == null)
                return NotFound();

            consumer.IsDisconnected = false;
            consumer.Status = "Active";

            var disconnection = await _context.Disconnections
                .Where(d => d.ConsumerId == id && !d.IsReconnected)
                .OrderByDescending(d => d.DateDisconnected)
                .FirstOrDefaultAsync();

            if (disconnection != null)
            {
                disconnection.IsReconnected = true;
                disconnection.DateReconnected = DateTime.Now;
            }

            // Store current date in a readable format
            var notificationDate = DateTime.Now.ToString("MMMM dd, yyyy");

            // ➕ In-App Notification
            var notif = new Notification
            {
                ConsumerId = consumer.Id,
                Title = "💧 Water Service Reconnected",
                Message = $"Hello {consumer.FirstName}, your water service has been successfully reconnected on {notificationDate}. Thank you for settling your bills.",
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notif);

            // Push Notification
            var user = consumer.User;
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
                    title = "💧 Water Service Reconnected",
                    body = $"Your water service has been reconnected on {notificationDate}. Thank you for settling your bills."
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

            // Audit Log
            var audit = new AuditTrail
            {
                Action = "Reconnect",
                PerformedBy = GetCurrentUsername(),
                Details = $"Reconnected Consumer ID {id}.",
                Timestamp = DateTime.Now
            };
            _context.AuditTrails.Add(audit);

            await _context.SaveChangesAsync();

            TempData["Message"] = $"Consumer {consumer.FirstName} has been reconnected and notified.";
            return RedirectToAction(nameof(Index));
        }




        //================== NOTIFY ACTIONS ==================

        // POST: Disconnection/Notify
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Notify(int id)
        {
            var consumer = await _context.Consumers
                .Include(c => c.Billings)
                .Include(c => c.User) // For push notification
                .FirstOrDefaultAsync(c => c.Id == id);

            if (consumer != null)
            {
                var overdueBills = consumer.Billings
                    .Where(b => !b.IsPaid && b.DueDate < DateTime.Today)
                    .ToList();

                if (overdueBills.Count >= 2)
                {
                    var disconnectionDate = DateTime.Today.AddDays(3).ToString("MMMM dd, yyyy");

                    var notif = new Notification
                    {
                        ConsumerId = consumer.Id,
                        Title = "⚠️ Disconnection Notice",
                        Message = $"Hello {consumer.FirstName}, you have 2 overdue bills that are not yet paid. Please pay at least one bill on or before {disconnectionDate} to avoid disconnection.",
                        CreatedAt = DateTime.Now
                    };

                    _context.Notifications.Add(notif);

                    // Push Notification
                    var user = consumer.User;
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
                            title = "⚠️ Disconnection Warning",
                            body = $"You have 2 unpaid overdue bills. Pay on or before {disconnectionDate} to avoid disconnection."
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

                    // Audit log
                    var audit = new AuditTrail
                    {
                        Action = "Notify",
                        PerformedBy = GetCurrentUsername(),
                        Details = $"Sent disconnection notice to Consumer ID {id}.",
                        Timestamp = DateTime.Now
                    };
                    _context.AuditTrails.Add(audit);

                    await _context.SaveChangesAsync();
                    TempData["Message"] = $"Disconnection notice sent to {consumer.FirstName}.";
                }
                else
                {
                    TempData["Error"] = "Consumer does not meet disconnection criteria (must have 2 or more overdue bills).";
                }
            }
            else
            {
                TempData["Error"] = "Consumer not found.";
            }

            return RedirectToAction("Index");
        }
    }
}
