using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class Payment
    {
        public int Id { get; set; }

        [Required]
        public int ConsumerId { get; set; }
        public Consumer? Consumer { get; set; }

        [Required]
        public int BillingId { get; set; }
        public Billing? Billing { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Required]
        [StringLength(50)]
        public string Method { get; set; } = string.Empty;

        [StringLength(100)]
        public string? TransactionId { get; set; }

        public bool IsVerified { get; set; } = false;

        // Store the filename or path of the uploaded receipt
        [StringLength(255)]
        public string? ReceiptPath { get; set; }
        public string? ProcessedBy { get; set; }


        // This property is NOT stored in the DB
        [NotMapped]
        public string? ImageUrl
        {
            get
            {
                if (string.IsNullOrEmpty(ReceiptPath))
                    return null;
                return $"/uploads/receipts/{ReceiptPath}";
            }
        }
    }
}
