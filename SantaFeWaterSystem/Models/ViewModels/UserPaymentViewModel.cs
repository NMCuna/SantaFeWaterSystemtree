using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SantaFeWaterSystem.Models.ViewModels
{
  public class UserPaymentViewModel
{
          public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;


        // Optional: Computed property to keep compatibility with old FullName
        public string FullName => $"{FirstName} {MiddleName} {LastName}".Replace("  ", " ").Trim();
        public Consumer Consumer { get; set; }
    public List<Billing> PendingBills { get; set; }
    public List<Payment> PreviousPayments { get; set; }


   
        // 👇 Add these two new properties
        public int PaidBillsCount { get; set; }
        public int UnpaidBillsCount { get; set; }

    }

}
