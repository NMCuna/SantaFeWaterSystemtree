using System;

namespace SantaFeWaterSystem.Models
{
    public class Backup
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public DateTime BackupDate { get; set; }
        public string FilePath { get; set; }
    }
}
