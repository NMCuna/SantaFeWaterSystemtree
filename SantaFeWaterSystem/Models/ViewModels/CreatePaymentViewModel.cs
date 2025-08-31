using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace SantaFeWaterSystem.Models.ViewModels
{
    public class CreatePaymentViewModel
    {
        public int PaymentId { get; set; }
        public int ConsumerId { get; set; }
        public int BillingId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public string? Method { get; set; }
        public string? TransactionId { get; set; }
        public string? BillNo { get; set; }


        // For uploading new receipt
        public IFormFile? ReceiptImageFile { get; set; }

        // For displaying current receipt image
        public string? ExistingReceiptImageUrl { get; set; }

        public List<SelectListItem>? Consumers { get; set; }
        public List<SelectListItem>? Billings { get; set; }
    }
}
