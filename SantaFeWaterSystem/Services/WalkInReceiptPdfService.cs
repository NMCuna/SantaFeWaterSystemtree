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
            // ✅ Load logo from wwwroot/images/logo.png
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            byte[]? logoData = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A6); // ✅ Small receipt size
                    page.DefaultTextStyle(x => x.FontFamily("Courier New").FontSize(10));

                    // ===== HEADER =====
                    page.Header().Column(header =>
                    {
                        if (logoData != null)
                        {
                            header.Item().AlignCenter().Height(50).Image(logoData);
                        }

                        header.Item().AlignCenter().Text("Santa Fe Water System")
                            .SemiBold().FontSize(12);

                        header.Item().AlignCenter().Text("OFFICIAL RECEIPT")
                            .Bold().FontSize(11);

                        header.Item().AlignCenter().Text("-------------------------------")
                            .FontSize(9);
                    });

                    // ===== CONTENT =====
                    page.Content().Column(col =>
                    {
                        col.Spacing(2);

                        col.Item().Text($"Date Paid: {payment.PaymentDate:MMM dd, yyyy hh:mm tt}");
                        col.Item().Text($"Consumer Name: {consumer.FirstName} {consumer.LastName}");
                        col.Item().Text($"Account No: {consumer.User?.AccountNumber}");

                        col.Item().Text("-----------------------------------------------")
                            .FontSize(9);

                        col.Item().Text($"Billing Date: {billing.BillingDate:MMM dd, yyyy}");
                        col.Item().Text($"Cubic Meter Used: {billing.CubicMeterUsed}");
                        col.Item().Text($"Amount Due:    ₱{billing.AmountDue:N2}");
                        col.Item().Text($"Add. Fees:    ₱{billing.AdditionalFees:N2}");

                        col.Item().Text($"TOTAL PAID:   ₱{payment.AmountPaid:N2}")
                            .Bold().FontSize(12);

                        col.Item().Text("-----------------------------------------------")
                            .FontSize(9);

                        col.Item().Text($"Payment Method: {payment.Method ?? "Cash"}");
                        col.Item().Text($"Trans. ID: {payment.TransactionId ?? "-"}");
                    });

                    // ===== FOOTER =====
                    page.Footer().Column(footer =>
                    {
                        footer.Item().AlignCenter().Text("Thank you for your payment!")
                            .Bold().FontSize(10);

                        footer.Item().AlignCenter().PaddingTop(10).Column(sig =>
                        {
                            sig.Item().AlignCenter().Width(120).LineHorizontal(1);
                            sig.Item().AlignCenter().Text("Authorized Signature").FontSize(8);
                        });

                        footer.Item().AlignCenter().Text("-------------------------------")
                            .FontSize(9);

                        footer.Item().AlignCenter().Text(x =>
                        {
                            x.Span("Generated: ").FontSize(8);
                            x.Span(DateTime.Now.ToString("g")).SemiBold().FontSize(8);
                        });
                    });
                });
            }).GeneratePdf();
        }
    }
}
