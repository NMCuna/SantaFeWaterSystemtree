using System;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.ViewModels;
using SantaFeWaterSystem.Models;

public class MonthlyBillingPdfDocument : IDocument
{
    private readonly MonthlyBillingSummaryViewModel _model;

    public MonthlyBillingPdfDocument(MonthlyBillingSummaryViewModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        // First Page: Summary
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(12));

            page.Header()
                .Text($"Monthly Billing Summary - {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(_model.Month)} {_model.Year}")
                .SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);

            page.Content()
                .Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text("Summary Section").Bold().FontSize(14);

                    var totalBilled = _model.Billings.Sum(b => b.TotalAmount);
                    var totalPaid = _model.Billings.Where(b => b.Status == "Paid").Sum(b => b.TotalAmount);
                    var totalUnpaid = _model.Billings.Where(b => b.Status == "Unpaid").Sum(b => b.TotalAmount);
                    var totalPending = _model.Billings.Where(b => b.Status == "Pending").Sum(b => b.TotalAmount);

                    col.Item().Text($"Total Billed: {totalBilled:C}");
                    col.Item().Text($"Total Paid: {totalPaid:C}");
                    col.Item().Text($"Total Unpaid: {totalUnpaid:C}");
                    col.Item().Text($"Total Pending: {totalPending:C}");
                });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span("Generated on ").FontSize(10);
                x.Span(DateTime.Now.ToString("MMMM dd, yyyy")).SemiBold().FontSize(10);
            });

        });

        // Additional Pages: Details by Status Group
        var groupedByStatus = _model.Billings
            .GroupBy(b => b.Status)
            .OrderBy(g => g.Key);

        foreach (var group in groupedByStatus)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Text($"{group.Key} Billings - {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(_model.Month)} {_model.Year}")
                    .SemiBold().FontSize(16).FontColor(Colors.Black);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();     // Consumer
                        columns.ConstantColumn(80);   // Billing Date
                        columns.ConstantColumn(50);   // Used
                        columns.ConstantColumn(80);   // Due
                        columns.ConstantColumn(70);   // Penalty
                        columns.ConstantColumn(90);   // Total
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Consumer").Bold();
                        header.Cell().Text("Date").Bold();
                        header.Cell().Text("Used").Bold();
                        header.Cell().Text("Due").Bold();
                        header.Cell().Text("Penalty").Bold();
                        header.Cell().Text("Total").Bold();
                    });

                    foreach (var bill in group)
                    {
                        var consumerName = bill.Consumer != null
                            ? $"{bill.Consumer.FirstName} {bill.Consumer.LastName}"
                            : "Unknown";

                        table.Cell().Text(consumerName);
                        table.Cell().Text(bill.BillingDate.ToString("MM/dd/yyyy"));
                        table.Cell().Text($"{bill.CubicMeterUsed} m³");
                        table.Cell().Text($"{bill.AmountDue:C}");
                        table.Cell().Text($"{bill.Penalty:C}");
                        table.Cell().Text($"{bill.TotalAmount:C}");
                    }
                });

                page.Footer().Element(footer =>
                {
                    footer.AlignCenter().Text($"Page - {group.Key}").FontSize(10);
                });
            });
        }
    }
}
