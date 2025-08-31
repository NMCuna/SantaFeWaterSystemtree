using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SantaFeWaterSystem.Models
{
    public class Rate
    {
        public int Id { get; set; }

       
        [Required]
        public ConsumerType AccountType { get; set; }

        [Required]
        [Display(Name = "Rate Per Cubic Meter")]
        [Range(0, double.MaxValue)]
        [Precision(18, 2)]
        public decimal RatePerCubicMeter { get; set; }

        [Required]
        [Display(Name = "Penalty Amount")]
        [Range(0, double.MaxValue)]
        [Precision(18, 2)]
        public decimal PenaltyAmount { get; set; }

        [Required]
        [Display(Name = "Effective Date")]
        [DataType(DataType.Date)]
        public DateTime EffectiveDate { get; set; } = DateTime.Now;
    }

}
