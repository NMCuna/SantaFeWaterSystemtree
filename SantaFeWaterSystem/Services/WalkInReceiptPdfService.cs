using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using System;
using System.IO;

namespace SantaFeWaterSystem.Services
{
    public static class WalkInReceiptPdfService
    {
        public static byte[] Generate(Payment payment, Consumer consumer, Billing billing)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header().Row(row =>
                    {
                        row.ConstantItem(60).Image("wwwroot/images/logo.png");

                        row.RelativeItem().AlignCenter().Text("Santa Fe Water System - Walk-in Payment Receipt")
                            .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(5);

                        col.Item().Text($"Date: {payment.PaymentDate:MMMM dd, yyyy hh:mm tt}");
                        col.Item().Text($"Account Name: {consumer.FirstName}");
                        col.Item().Text($"Account Number: {consumer.User?.AccountNumber}");

                        col.Item().LineHorizontal(1);

                        col.Item().Text($"Billing Date: {billing.BillingDate:MMMM dd, yyyy}");
                        col.Item().Text($"Cubic Meter Used: {billing.CubicMeterUsed}");
                        col.Item().Text($"Amount Due: ₱{billing.AmountDue:N2}");
                        col.Item().Text($"Additional Fees: ₱{billing.AdditionalFees:N2}");
                        col.Item().Text($"Total Paid: ₱{payment.AmountPaid:N2}").Bold();
                        col.Item().Text($"Payment Method: {payment.Method ?? "Cash"}");
                        col.Item().Text($"Transaction ID: {payment.TransactionId ?? "-"}");
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Generated on ").FontSize(10);
                        x.Span(DateTime.Now.ToString("f")).SemiBold().FontSize(10);
                    });
                });
            }).GeneratePdf();
        }
    }
}
