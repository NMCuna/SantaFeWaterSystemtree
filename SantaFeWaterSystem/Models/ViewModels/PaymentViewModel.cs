namespace SantaFeWaterSystem.Models.ViewModels
{
    public class PaymentViewModel
    {
        public int PaymentId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;


        // Optional: Computed property to keep compatibility with old FullName
        public string FullName => $"{FirstName} {MiddleName} {LastName}".Replace("  ", " ").Trim();
        public DateTime BillingDate { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? TransactionId { get; set; }
        public string? ReceiptImageUrl { get; set; }
        public bool IsVerified { get; set; }
        public string? BillNo { get; set; }

    }
}
