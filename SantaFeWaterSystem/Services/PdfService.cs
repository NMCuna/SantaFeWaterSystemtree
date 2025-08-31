using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.Models;
using System.Globalization;

public class PdfService
{
    public byte[] GeneratePaymentsPdf(List<Payment> payments)
    {
        var culture = new CultureInfo("fil-PH");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);

                page.Header()
                    .Text("Selected Payments Report")
                    .FontSize(20)
                    .Bold()
                    .FontColor(Colors.Blue.Medium);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();     // Consumer
                        columns.ConstantColumn(100);  // Date
                        columns.ConstantColumn(100);  // Amount
                        columns.ConstantColumn(80);   // Method
                        columns.ConstantColumn(80);   // Status
                    });

                    // Header row
                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Consumer");
                        header.Cell().Element(CellStyle).Text("Payment Date");
                        header.Cell().Element(CellStyle).Text("Amount Paid");
                        header.Cell().Element(CellStyle).Text("Method");
                        header.Cell().Element(CellStyle).Text("Status");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container
                                .DefaultTextStyle(x => x.SemiBold())
                                .PaddingVertical(5)
                                .Background(Colors.Grey.Lighten2)
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Medium);
                        }
                    });

                    // Data rows
                    foreach (var p in payments)
                    {
                        table.Cell().Text($"{p.Billing.Consumer.LastName}, {p.Billing.Consumer.FirstName}");
                        table.Cell().Text(p.PaymentDate.ToShortDateString());
                        table.Cell().Text(p.AmountPaid.ToString("C", culture));
                        table.Cell().Text(p.Method ?? "-");
                        table.Cell().Text(p.IsVerified ? "Verified" : "Pending");
                    }
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.DefaultTextStyle(x => x.FontSize(10));
                    txt.Span("Generated on ");
                    txt.Span(DateTime.Now.ToString("f")).SemiBold();
                });

            });
        });

        return document.GeneratePdf();
    }
}
