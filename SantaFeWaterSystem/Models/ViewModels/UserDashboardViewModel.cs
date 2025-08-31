using SantaFeWaterSystem.Models;
using System.Collections.Generic;

namespace SantaFeWaterSystem.ViewModels
{
    public class UserDashboardViewModel
    {
        public Consumer Consumer { get; set; }
        public List<Billing> RecentBills { get; set; }
        public List<Payment> RecentPayments { get; set; }
        public List<Support> RecentSupportTickets { get; set; }
        public List<Notification> Notifications { get; set; }

    }
}
