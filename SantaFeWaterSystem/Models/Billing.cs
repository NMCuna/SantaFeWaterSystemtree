using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class Billing
    {      
        public int Id { get; set; }

        [Required]
        public int ConsumerId { get; set; }

        // Navigation property
        public Consumer? Consumer { get; set; } 
        public string? BillNo { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Billing Date")]
        public DateTime BillingDate { get; set; }
     
        public string? Remarks { get; set; }

        [Required]
        [Display(Name = "Previous Reading")]
        [Precision(18, 2)]
        public decimal PreviousReading { get; set; }

        [Required]
        [Display(Name = "Present Reading")]
        [Precision(18, 2)]
        public decimal PresentReading { get; set; }

        [Required]
        [Display(Name = "Cubic Meter Used")]
        [Precision(18, 2)]
        public decimal CubicMeterUsed { get; set; }

        [Required]
        [Display(Name = "Amount Due")]
        [Precision(18, 2)]
        public decimal AmountDue { get; set; }

        [Display(Name = "Penalty")]
        [Precision(18, 2)]
        public decimal Penalty { get; set; } = 0;


        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }


        [Display(Name = "Additional Fees")]
        [Range(0, double.MaxValue)]
        [Precision(18, 2)]
        public decimal? AdditionalFees { get; set; } = 0;

        [Display(Name = "Total Amount")]
        [Range(0, double.MaxValue)]
        [Precision(18, 2)]
        public decimal TotalAmount { get; set; } = 0;

        [Display(Name = "Is Paid")]
        public bool IsPaid { get; set; } = false;

        [Display(Name = "Status")]
        [MaxLength(20)]
        public string Status { get; set; } = "Unpaid"; // Options: "Unpaid", "Paid", "Overdue", etc.

        // Optional: Add a helper method to keep IsPaid and Status in sync
        public void MarkAsPaid()
        {
            IsPaid = true;
            Status = "Paid";
        }

        public void MarkAsUnpaid()
        {
            IsPaid = false;
            Status = "Unpaid";
        }


        [NotMapped]
        public bool HasPendingPayment { get; set; }     
        public ICollection<Disconnection>? Disconnections { get; set; }

    }
}

