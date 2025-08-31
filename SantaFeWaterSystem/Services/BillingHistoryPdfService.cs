using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.ViewModels;
using System;
using System.Linq;

namespace SantaFeWaterSystem.Services
{
    public static class BillingHistoryPdfService
    {
        public static byte[] Generate(BillingHistoryViewModel model, string? searchTerm, string? statusFilter)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    // 📌 Header Section
                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text("SANTA FE WATER BILLING HISTORY")
                            .FontSize(16).Bold().FontColor(Colors.Blue.Medium);

                        if (!string.IsNullOrWhiteSpace(searchTerm) || !string.IsNullOrWhiteSpace(statusFilter))
                        {
                            string filterText = "Filtered by:";
                            if (!string.IsNullOrWhiteSpace(statusFilter))
                                filterText += $" Status = {statusFilter}";
                            if (!string.IsNullOrWhiteSpace(searchTerm))
                                filterText += $", Search = '{searchTerm}'";

                            header.Item().AlignCenter().Text(filterText)
                                .FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
                        }
                    });

                    // 📄 Table Section
                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // Account Number
                                columns.RelativeColumn(2); // Full Name
                                columns.RelativeColumn(1.2f); // Billing Date
                                columns.RelativeColumn(1.2f); // Due Date
                                columns.RelativeColumn(1); // Amount
                                columns.RelativeColumn(1); // Status
                            });

                            // 📋 Table Header
                            table.Header(header =>
                            {
                                header.Cell().Element(CellHeaderStyle).Text("Account No");
                                header.Cell().Element(CellHeaderStyle).Text("Full Name");
                                header.Cell().Element(CellHeaderStyle).Text("Billing Date");
                                header.Cell().Element(CellHeaderStyle).Text("Due Date");
                                header.Cell().Element(CellHeaderStyle).Text("Amount");
                                header.Cell().Element(CellHeaderStyle).Text("Status");
                            });

                            // 🔄 Table Rows
                            foreach (var bill in model.Billings)
                            {
                                table.Cell().Element(CellBodyStyle).Text(bill.AccountNumber);
                                table.Cell().Element(CellBodyStyle).Text(bill.FullName);
                                table.Cell().Element(CellBodyStyle).Text(bill.BillingDate.ToString("MMM dd, yyyy"));
                                table.Cell().Element(CellBodyStyle).Text(bill.DueDate.ToString("MMM dd, yyyy"));
                                table.Cell().Element(CellBodyStyle).Text($"₱{bill.AmountDue:N2}");
                                table.Cell().Element(CellBodyStyle).Text(bill.Status);
                            }

                            // 💡 Cell styles
                            static IContainer CellHeaderStyle(IContainer container) =>
                                container.Padding(5).Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Medium);

                            static IContainer CellBodyStyle(IContainer container) =>
                                container.Padding(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        });
                    });

                    // 📅 Footer Section
                    page.Footer()
                        .AlignCenter()
                        .Text(txt =>
                        {
                            txt.Span("Generated on ").FontSize(9);
                            txt.Span(DateTime.Now.ToString("f")).FontSize(9).SemiBold();
                        });
                });
            }).GeneratePdf();
        }
    }
}
