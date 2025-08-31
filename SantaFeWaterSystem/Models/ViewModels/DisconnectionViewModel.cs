using System;

namespace SantaFeWaterSystem.ViewModels
{
    public class DisconnectionViewModel
    {
        public int ConsumerId { get; set; }
        public string ConsumerName { get; set; }
        public int OverdueBillsCount { get; set; }
        public decimal TotalUnpaidAmount { get; set; }
        public DateTime? LatestDueDate { get; set; }
        public bool IsDisconnected { get; set; }
    }
}
