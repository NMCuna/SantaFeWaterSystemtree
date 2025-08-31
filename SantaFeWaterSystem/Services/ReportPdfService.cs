using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.Models;
using System.Globalization;

namespace SantaFeWaterSystem.Services
{
    public static class ReportPdfService
    {
        private static IContainer CellStyle(IContainer container)
        {
            return container
                .PaddingVertical(2)
                .PaddingHorizontal(4)
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2);
        }

        public static byte[] GenerateReport(List<Billing> billings, List<Payment> payments, string logoPath, decimal totalCubicMeterUsed)
        {
            decimal totalPaid = payments.Sum(p => p.AmountPaid);
            decimal totalBilled = billings.Sum(b => b.TotalAmount);
            decimal totalUnpaid = billings
                .Where(b => b.Status == "Unpaid" || b.Status == "Pending")
                .Sum(b => b.TotalAmount);
            int totalDisconnections = billings.Count(b => b.Status == "Disconnected");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Row(row =>
                        {
                            row.ConstantItem(80).Image(logoPath);
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Santa Fe Water Billing System").FontSize(18).Bold().FontColor(Colors.Blue.Medium);
                                col.Item().Text($"Report Date: {DateTime.Now:MMMM dd, yyyy}");
                            });
                        });

                    page.Content()
                        .PaddingVertical(10)
                        .Column(col =>
                        {
                            col.Item().Element(x => x.PaddingBottom(5)).Text("Summary").FontSize(14).Bold().Underline();
                            col.Item().Table(summary =>
                            {
                                summary.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                summary.Cell().Element(CellStyle).Text("Total Paid:");
                                summary.Cell().Element(CellStyle).Text($"₱{totalPaid:N2}");

                                summary.Cell().Element(CellStyle).Text("Total Billed:");
                                summary.Cell().Element(CellStyle).Text($"₱{totalBilled:N2}");

                                summary.Cell().Element(CellStyle).Text("Total Unpaid:");
                                summary.Cell().Element(CellStyle).Text($"₱{totalUnpaid:N2}");

                                summary.Cell().Element(CellStyle).Text("Total Disconnections:");
                                summary.Cell().Element(CellStyle).Text($"{totalDisconnections}");

                                summary.Cell().Element(CellStyle).Text("Total Cubic Meter Used:");
                                summary.Cell().Element(CellStyle).Text($"{totalCubicMeterUsed:N2} m³");
                            });

                            col.Item().PaddingVertical(10);

                            // Billing Table
                            col.Item().Element(x => x.PaddingBottom(5)).Text("Billing Details").FontSize(14).Bold().Underline();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(); // Consumer
                                    columns.RelativeColumn(); // Billing Date
                                    columns.RelativeColumn(); // Status
                                    columns.RelativeColumn(); // Usage
                                    columns.RelativeColumn(); // Amount
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Consumer");
                                    header.Cell().Element(CellStyle).Text("Billing Date");
                                    header.Cell().Element(CellStyle).Text("Status");
                                    header.Cell().Element(CellStyle).Text("Usage (m³)");
                                    header.Cell().Element(CellStyle).Text("Total Amount");
                                });

                                foreach (var b in billings.OrderByDescending(b => b.BillingDate))
                                {
                                    table.Cell().Element(CellStyle).Text(b.Consumer?.FirstName ?? "Unknown");
                                    table.Cell().Element(CellStyle).Text(b.BillingDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Element(CellStyle).Text(b.Status);
                                    table.Cell().Element(CellStyle).Text(b.CubicMeterUsed.ToString());
                                    table.Cell().Element(CellStyle).Text($"₱{b.TotalAmount:N2}");
                                }
                            });

                            col.Item().PaddingVertical(10);

                            // Payments Table
                            col.Item().Element(x => x.PaddingBottom(5)).Text("Payments Received").FontSize(14).Bold().Underline();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(); // Consumer
                                    columns.RelativeColumn(); // Payment Date
                                    columns.RelativeColumn(); // Amount Paid
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Consumer");
                                    header.Cell().Element(CellStyle).Text("Payment Date");
                                    header.Cell().Element(CellStyle).Text("Amount Paid");
                                });

                                foreach (var p in payments.OrderByDescending(p => p.PaymentDate))
                                {
                                    table.Cell().Element(CellStyle).Text(p.Consumer?.FirstName ?? "Unknown");
                                    table.Cell().Element(CellStyle).Text(p.PaymentDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Element(CellStyle).Text($"₱{p.AmountPaid:N2}");
                                }
                            });
                        });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .Text(txt =>
                        {
                            txt.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken1));
                            txt.Span("Generated by Santa Fe Water System • ");
                            txt.Span(DateTime.Now.ToString("f", CultureInfo.InvariantCulture));
                        });
                });
            }).GeneratePdf();
        }
    }
}