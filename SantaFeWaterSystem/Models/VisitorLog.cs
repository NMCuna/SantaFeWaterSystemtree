using System;


namespace SantaFeWaterSystem.Models
{
    public class VisitorLog
    {
        public int Id { get; set; }
        public string IpAddress { get; set; } = string.Empty;

        // Store date-only (local Philippine date at 00:00)
        public DateTime VisitDateLocal { get; set; } // date column
        public DateTime VisitedAtUtc { get; set; }   // audit
    }
}
