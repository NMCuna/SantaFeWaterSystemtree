// Services/BillingService.cs
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace SantaFeWaterSystem.Services
{
    public class BillingService
    {
        private readonly ApplicationDbContext _context;

        public BillingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RevertBillingPaymentStatus(int billingId)
        {
            var billing = await _context.Billings.FindAsync(billingId);
            if (billing != null)
            {
                billing.Status = "Unpaid";
                billing.IsPaid = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}
