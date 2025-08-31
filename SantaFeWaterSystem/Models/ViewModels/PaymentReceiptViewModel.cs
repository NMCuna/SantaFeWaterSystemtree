using System;

namespace SantaFeWaterSystem.ViewModels
{
    public class PaymentReceiptViewModel
    {
        public int PaymentId { get; set; }
        public string AccountNumber { get; set; }
        public string FullName { get; set; }
        public string BillNo { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public string Method { get; set; }
        public string TransactionId { get; set; }
        public string Status { get; set; }
    }
}
