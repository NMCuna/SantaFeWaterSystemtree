using SantaFeWaterSystem.Models;

public class MonthlyBillingSummaryViewModel
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<Billing> Billings { get; set; }
}
