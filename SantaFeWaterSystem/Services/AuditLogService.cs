using Microsoft.AspNetCore.Http;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.Services
{
    public class AuditLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string details, string? performedBy = null)
        {
            // Use current user's identity if not provided
            if (string.IsNullOrWhiteSpace(performedBy))
            {
                performedBy = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
            }
            // Convert UTC to Philippine Time
            var phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var timestampPH = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);

            _context.AuditTrails.Add(new AuditTrail
            {
                Action = action,
                Details = details,
                PerformedBy = performedBy,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }
    }
}  
