namespace SantaFeWaterSystem.Models
{
    public class SmsLog
    {
        public int Id { get; set; }
        public int? ConsumerId { get; set; }
        public string ContactNumber { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ResponseMessage { get; set; }

        public Consumer? Consumer { get; set; }
        // ✅ New field
        public int RetryCount { get; set; }
        public bool IsArchived { get; set; } = false;
    }

}
