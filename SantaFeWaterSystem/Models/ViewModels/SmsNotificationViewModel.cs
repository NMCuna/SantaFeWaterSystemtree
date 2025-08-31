// Models/ViewModels/SmsNotificationViewModel.cs
using System.Collections.Generic;
using SantaFeWaterSystem.Models;

namespace SantaFeWaterSystem.Models.ViewModels
{
    public class SmsNotificationViewModel
    {
        public List<Consumer> ConsumersWithBills { get; set; } = new();
        public List<int> SelectedConsumerIds { get; set; } = new();
        public string Message { get; set; } = string.Empty;

        public bool SendToAll { get; set; }

        // For pagination & search
        public string SearchTerm { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;
    }

}
