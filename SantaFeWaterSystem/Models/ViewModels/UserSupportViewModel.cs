namespace SantaFeWaterSystem.Models.ViewModels
{
    // ViewModels/UserSupportViewModel.cs
    public class UserSupportViewModel
    {
        public string Subject { get; set; }
        public string Message { get; set; }

        public List<Support>? PreviousTickets { get; set; }
    }

}
