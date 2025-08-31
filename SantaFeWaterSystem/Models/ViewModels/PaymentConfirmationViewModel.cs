namespace SantaFeWaterSystem.ViewModels
{
    public class PaymentConfirmationViewModel
    {
        public string? TransactionId { get; set; } // Used as Reference Number
        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Method { get; set; } = string.Empty;
    }
}
