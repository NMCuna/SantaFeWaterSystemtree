using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models.ViewModels
{
  public class PayViewModel
    {
        public int BillingId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;


        // Optional: Computed property to keep compatibility with old FullName
        public string FullName => $"{FirstName} {MiddleName} {LastName}".Replace("  ", " ").Trim();
        public string ConsumerName { get; set; }
        public string AccountNumber { get; set; }
        public decimal PreviousReading { get; set; }
        public decimal PresentReading { get; set; }
        public decimal CubicMeterUsed => PresentReading - PreviousReading;
        public decimal AmountDue { get; set; }
        public decimal AdditionalFee { get; set; }
        public decimal Penalty { get; set; }
        public decimal TotalAmount => AmountDue + AdditionalFee + Penalty;
        public string TransactionId { get; set; }

        // New: Payment method selected by user
        [Required(ErrorMessage = "Please select a payment method.")]
        public string SelectedPaymentMethod { get; set; }

        // QR code URLs for GCash and Maya
        public string GcashQrImageUrl { get; set; } = "/images/gcash-qr.png";
        public string MayaQrImageUrl { get; set; } = "/images/maya-qr.png";

        [Required(ErrorMessage = "Please upload a receipt image.")]
        public IFormFile Receipt { get; set; }
    }

}
