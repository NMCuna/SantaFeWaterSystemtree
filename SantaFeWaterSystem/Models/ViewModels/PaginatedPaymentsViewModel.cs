using Microsoft.EntityFrameworkCore;

namespace SantaFeWaterSystem.Models.ViewModels
{
    public class PaginatedPaymentsViewModel
    {
        public List<PaymentViewModel> Payments { get; set; }

        public int PageNumber { get; set; }
        public int TotalPages { get; set; }

        public string SearchTerm { get; set; }
        public string StatusFilter { get; set; }

        // NEW
        public string PaymentMethodFilter { get; set; }

        public string? BillNo { get; set; }
        public int? SelectedMonth { get; set; }
        public int? SelectedYear { get; set; }
    }
}
    