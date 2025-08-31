using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;

namespace SantaFeWaterSystem.Services
{
    public class PaymentPdfService
    {
        public static byte[] Generate(List<PaymentViewModel> payments, string searchTerm, string statusFilter)
        {
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    // Header Section
                    page.Header().Column(header =>
                    {
                        header.Item().Row(row =>
                        {
                            row.ConstantItem(60).Image(logoPath, ImageScaling.FitHeight);
                            row.RelativeItem().AlignRight().Text("Payment Report")
                                .FontSize(20).Bold().FontColor(Colors.Blue.Medium);
                        });

                        if (!string.IsNullOrWhiteSpace(statusFilter) || !string.IsNullOrWhiteSpace(searchTerm))
                        {
                            var filterText = "Filters Applied: ";
                            if (!string.IsNullOrWhiteSpace(searchTerm))
                                filterText += $"Search - '{searchTerm}' ";
                            if (!string.IsNullOrWhiteSpace(statusFilter))
                                filterText += $" | Status - {statusFilter}";

                            header.Item().PaddingTop(5).Text(filterText).FontSize(10).Italic();
                        }

                        header.Item().Text($"Generated on: {DateTime.Now:f}")
                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                    });

                    // Table Section
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Consumer
                            columns.RelativeColumn(1); // Billing Date
                            columns.RelativeColumn(1); // Amount Paid
                            columns.RelativeColumn(1); // Method
                            columns.RelativeColumn(1); // Status
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Consumer").Bold();
                            header.Cell().Element(CellStyle).Text("Billing Date").Bold();
                            header.Cell().Element(CellStyle).Text("Amount Paid").Bold();
                            header.Cell().Element(CellStyle).Text("Method").Bold();
                            header.Cell().Element(CellStyle).Text("Status").Bold();

                            static IContainer CellStyle(IContainer container)
                            {
                                return container.DefaultTextStyle(x => x.SemiBold())
                                                .Padding(5)
                                                .Background(Colors.Grey.Lighten3)
                                                .BorderBottom(1)
                                                .BorderColor(Colors.Grey.Medium);
                            }
                        });

                        foreach (var p in payments)
                        {
                            table.Cell().Padding(5).Text(p.FullName ?? "-");
                            table.Cell().Padding(5).Text(p.BillingDate.ToShortDateString());
                            table.Cell().Padding(5).Text($"₱{p.AmountPaid:N2}");
                            table.Cell().Padding(5).Text(p.PaymentMethod ?? "Cash");
                            table.Cell().Padding(5).Text(p.IsVerified ? "Verified" : "Pending");
                        }
                    });

                    // Footer
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
