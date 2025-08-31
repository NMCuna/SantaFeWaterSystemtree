using System;
using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.ViewModels
{
    public class BillingViewModel
    {
        public int Id { get; set; }

        public string BillNo { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Billing Date")]
        public DateTime BillingDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        [Required]
        [Display(Name = "Previous Reading")]
        public decimal PreviousReading { get; set; }

        [Required]
        [Display(Name = "Present Reading")]
        public decimal PresentReading { get; set; }

        [Required]
        [Display(Name = "Cubic Meter Used")]
        public decimal CubicMeterUsed { get; set; }

        [Required]
        [Display(Name = "Amount Due")]
        public decimal AmountDue { get; set; }

        [Display(Name = "Penalty")]
        public decimal Penalty { get; set; } = 0;

        [Display(Name = "Additional Fees")]
        [Range(0, double.MaxValue)]
        public decimal? AdditionalFees { get; set; } = 0;

        [Display(Name = "Total Amount")]
        [Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; } = 0;

        public string Status { get; set; } = string.Empty;

        // Additional metadata from related entities
        public string AccountNumber { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;
    }
}
