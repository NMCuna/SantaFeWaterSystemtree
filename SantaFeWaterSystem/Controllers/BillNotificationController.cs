using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;

public class BillNotificationController : Controller
{
    private readonly ApplicationDbContext _context;

    public BillNotificationController(ApplicationDbContext context)
    {
        _context = context;
    }


    // ================== SendPendingNotifications ==================

    [HttpPost]
    public async Task<IActionResult> SendPendingNotifications()
    {
        var pushClient = new PushServiceClient();

        pushClient.DefaultAuthentication = new VapidAuthentication(
            "BA_B1RL8wfVkIA7o9eZilYNt7D0_CbU5zsvqCZUFcCnVeqFr6a9BPxHPtWlNNgllEkEqk6jcRgp02ypGhGO3gZI", // Public
            "0UqP8AfB9hFaQhm54rEabEwlaCo44X23BO6ID8n7E_U"                                               // Private
        )
        {
            Subject = "mailto:cunanicolemichael@gmail.com"
        };

        var unnotified = await _context.BillNotifications
            .Where(n => !n.IsNotified)
            .Include(n => n.Billing)
            .ToListAsync();

        foreach (var item in unnotified)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ConsumerId == item.ConsumerId);
            if (user == null) continue;

            var subscriptions = await _context.UserPushSubscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();

            bool anySuccess = false;

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

                var payload = JsonSerializer.Serialize(new
                {
                    title = "📢 New Water Bill",
                    body = $"Amount Due: ₱{item.Billing.AmountDue} - Due {item.Billing.DueDate:MMM dd}"
                });

                try
                {
                    await pushClient.RequestPushMessageDeliveryAsync(subscription, new PushMessage(payload));
                    anySuccess = true; //  at least one delivery succeeded
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Push Error for user {user.Id}]: {ex.Message}");
                }
            }

            if (anySuccess)
            {
                item.IsNotified = true; //  Only set to true if at least one was sent
            }
        }

        await _context.SaveChangesAsync();
        return Ok("✅ Push notifications sent for pending bills.");
    }




   
    // ================== PUSH NOTIFICATION ==================

    [HttpPost]
    [Authorize(Roles = "User")] // Only logged-in users can save
    public async Task<IActionResult> SaveSubscription([FromBody] PushSubscriptionModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.Endpoint) || model.Keys == null)
        {
            return BadRequest("Invalid subscription data.");
        }

        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        int userId = int.Parse(userIdClaim);

        var exists = await _context.UserPushSubscriptions
            .AnyAsync(s => s.UserId == userId && s.Endpoint == model.Endpoint);

        if (!exists)
        {
            var sub = new UserPushSubscription
            {
                UserId = userId,
                Endpoint = model.Endpoint,
                P256DH = model.Keys.TryGetValue("p256dh", out var p256dh) ? p256dh : string.Empty,
                Auth = model.Keys.TryGetValue("auth", out var auth) ? auth : string.Empty
            };

            _context.UserPushSubscriptions.Add(sub);
            await _context.SaveChangesAsync();
        }

        return Ok();
    }


    // ================== PushSubscriptionModel ==================
    public class PushSubscriptionModel
    {
        public string? Endpoint { get; set; }
        public Dictionary<string, string>? Keys { get; set; }
    }
}
