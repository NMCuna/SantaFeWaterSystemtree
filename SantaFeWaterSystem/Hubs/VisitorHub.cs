using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SantaFeWaterSystem.Data;
using System;
using System.Linq;

namespace SantaFeWaterSystem.Hubs
{
    [Authorize(Roles = "Admin,Staff")]
    public class VisitorHub(ApplicationDbContext db) : Hub
    {
        public class VisitorCountsDto
        {
            public int Daily { get; set; }
            public int Monthly { get; set; }
            public int Yearly { get; set; }
        }

        public VisitorCountsDto GetCurrentCounts()
        {
            var tz = PhilippineTimeZone();
            var nowPh = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var today = nowPh.Date;

            var monthStart = new DateTime(nowPh.Year, nowPh.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);
            var yearStart = new DateTime(nowPh.Year, 1, 1);
            var nextYearStart = yearStart.AddYears(1);

            var daily = db.VisitorLogs.Count(v => v.VisitDateLocal == today);
            var monthly = db.VisitorLogs.Count(v => v.VisitDateLocal >= monthStart && v.VisitDateLocal < nextMonthStart);
            var yearly = db.VisitorLogs.Count(v => v.VisitDateLocal >= yearStart && v.VisitDateLocal < nextYearStart);

            return new VisitorCountsDto { Daily = daily, Monthly = monthly, Yearly = yearly };
        }

        // Helper that works on Windows/Linux
        private static TimeZoneInfo PhilippineTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
                catch { return TimeZoneInfo.CreateCustomTimeZone("PH", TimeSpan.FromHours(8), "Philippine Time", "Philippine Time"); }
            }
        }
    }
}
