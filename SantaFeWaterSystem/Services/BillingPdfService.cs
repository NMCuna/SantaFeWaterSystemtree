using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.ViewModels;
using System;

namespace SantaFeWaterSystem.Services
{
    public static class BillingPdfService
    {
        public static byte[] Generate(BillingViewModel model)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A5);

                    // Header
                    page.Header().AlignCenter().Text("SANTA FE WATER BILLING RECEIPT")
                        .FontSize(16).Bold().FontColor(Colors.Blue.Medium);

                    // Content
                    page.Content().Column(col =>
                    {
                        col.Spacing(5);

                        void AddLine(string label, string value, bool bold = false)
                        {
                            col.Item().Row(row =>
                            {
                                row.ConstantItem(140).Text(label).FontSize(11);

                                row.RelativeItem().Text(text =>
                                {
                                    if (bold)
                                        text.Span(value).Bold().FontSize(11);
                                    else
                                        text.Span(value).FontSize(11);
                                });
                            });
                        }


                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        AddLine("Account Number:", model.AccountNumber);
                        AddLine("Full Name:", model.FullName);
                        AddLine("Bill No:", model.BillNo ?? "-");
                        AddLine("Billing Date:", model.BillingDate.ToString("MMM dd, yyyy"));
                        AddLine("Due Date:", model.DueDate.ToString("MMM dd, yyyy"));
                        AddLine("Previous Reading:", model.PreviousReading.ToString());
                        AddLine("Present Reading:", model.PresentReading.ToString());
                        AddLine("Cubic Meter Used:", model.CubicMeterUsed.ToString());
                        AddLine("Amount Due:", $"₱{model.AmountDue:N2}");
                        AddLine("Penalty:", $"₱{model.Penalty:N2}");
                        AddLine("Additional Fees:", $"₱{model.AdditionalFees ?? 0:N2}");
                        AddLine("TOTAL AMOUNT:", $"₱{model.TotalAmount:N2}", bold: true);
                        AddLine("Status:", model.Status);

                        col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    // Footer
                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Generated on ").FontSize(9);
                        txt.Span(DateTime.Now.ToString("f")).FontSize(9).SemiBold();
                    });
                });
            }).GeneratePdf();
        }
    }
}
