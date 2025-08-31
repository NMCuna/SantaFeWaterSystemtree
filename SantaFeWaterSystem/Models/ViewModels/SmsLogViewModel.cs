namespace SantaFeWaterSystem.Models.ViewModels
{
    public class SmsLogViewModel
    {
        public int Id { get; set; } // Used for selection
        public string? ContactNumber { get; set; }
        public string? Message { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ResponseMessage { get; set; }
        public string? ConsumerName { get; set; }
        public bool IsArchived { get; set; } = false;

    }
}
