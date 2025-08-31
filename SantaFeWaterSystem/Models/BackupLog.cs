using System;

namespace SantaFeWaterSystem.Models
{
    public class BackupLog
    {
        public int Id { get; set; }
        public string? Action { get; set; } // "Backup" or "Restore"
        public string? FileName { get; set; }
        public string? PerformedBy { get; set; }
        public DateTime ActionDate { get; set; }
    }
}
