namespace SantaFeWaterSystem.Models
{
    public class EmailLog
    {
        public int Id { get; set; }
        public int ConsumerId { get; set; }
        public Consumer? Consumer { get; set; }
        public string? EmailAddress { get; set; }
        public string? Subject { get; set; }
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
        public string? ResponseMessage { get; set; }
        public DateTime SentAt { get; set; }
    }

}
