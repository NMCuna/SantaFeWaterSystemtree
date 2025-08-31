// BillingHistoryViewModel.cs
using System.Collections.Generic;

namespace SantaFeWaterSystem.ViewModels
{
    public class BillingHistoryViewModel
    {
        public List<BillingViewModel> Billings { get; set; } = new List<BillingViewModel>();

        // Additional consumer info for display/pdf
        public string ConsumerName { get; set; } = string.Empty;
        public string ConsumerAddress { get; set; } = string.Empty;
    }
}
