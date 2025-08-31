using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models
{
    public class Disconnection
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Consumer")]
        public int ConsumerId { get; set; }

        public Consumer Consumer { get; set; }

        [Required]
        public string Action { get; set; }  // "Disconnect" or "Reconnect"

      

        [Required]
        public DateTime DateDisconnected { get; set; }

        public DateTime? DateReconnected { get; set; }


        public string PerformedBy { get; set; }

        public string Remarks { get; set; }  // Optional notes

        public bool IsReconnected { get; set; }  // <-- Add this if needed

        public int? BillingId { get; set; }      // <-- Add this only if you're linking a billing
        public Billing Billing { get; set; }
    }
}

