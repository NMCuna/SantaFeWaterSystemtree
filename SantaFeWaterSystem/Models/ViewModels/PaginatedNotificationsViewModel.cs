namespace SantaFeWaterSystem.Models.ViewModels
{
    public class PaginatedNotificationsViewModel
    {
        public List<Notification> Notifications { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public string Filter { get; set; } // "", "unread", "archived"
    }
}
