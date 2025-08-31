using System.ComponentModel.DataAnnotations;

namespace SantaFeWaterSystem.Models
{
    public class BillNotification
    {
        public int Id { get; set; }

        [Required]
        public int BillingId { get; set; }
        public Billing Billing { get; set; }  // ✅ Navigation property

        [Required]
        public int ConsumerId { get; set; }
        public Consumer Consumer { get; set; } // ✅ Add this navigation property

        public bool IsNotified { get; set; } = false;
    }
}
