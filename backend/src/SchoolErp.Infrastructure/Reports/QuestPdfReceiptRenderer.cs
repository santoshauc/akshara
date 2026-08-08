using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolErp.Application.Fees.Queries;

namespace SchoolErp.Infrastructure.Reports;

/// <summary>
/// Renders fee receipts with QuestPDF (Community license — noted in
/// docs/security-notes.md). A5 landscape: compact enough to print two-up.
/// </summary>
public sealed class QuestPdfReceiptRenderer : IReceiptRenderer
{
    private const string Blue = "#1565C0";

    static QuestPdfReceiptRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ReceiptData data) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A5.Landscape());
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(10.5f));

            page.Header().Column(header =>
            {
                header.Item().Row(top =>
                {
                    top.RelativeItem().Column(school =>
                    {
                        school.Item().Text(data.SchoolName).FontSize(16).Bold().FontColor(Blue);
                        if (data.SchoolCity is { } city)
                        {
                            school.Item().Text(city).FontColor(Colors.Grey.Darken1).FontSize(9);
                        }
                    });
                    top.ConstantItem(150).AlignRight().Column(receipt =>
                    {
                        receipt.Item().AlignRight().Text("FEE RECEIPT").Bold().LetterSpacing(0.1f);
                        receipt.Item().AlignRight().Text(data.ReceiptNumber)
                            .FontColor(Blue).SemiBold();
                        receipt.Item().AlignRight()
                            .Text(data.PaidOn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture))
                            .FontColor(Colors.Grey.Darken1).FontSize(9);
                    });
                });
                header.Item().PaddingTop(8).LineHorizontal(1).LineColor(Blue);
            });

            page.Content().PaddingVertical(14).Column(content =>
            {
                content.Item().Row(details =>
                {
                    details.RelativeItem().Column(left =>
                    {
                        left.Item().Text(text =>
                        {
                            text.Span("Received from:  ").SemiBold();
                            text.Span(data.StudentName);
                        });
                        left.Item().Text(text =>
                        {
                            text.Span("Admission no:  ").SemiBold();
                            text.Span(data.AdmissionNumber);
                        });
                        if (data.ClassName is { } className)
                        {
                            left.Item().Text(text =>
                            {
                                text.Span("Class:  ").SemiBold();
                                text.Span(className);
                            });
                        }
                    });
                    details.RelativeItem().Column(right =>
                    {
                        right.Item().Text(text =>
                        {
                            text.Span("Mode:  ").SemiBold();
                            text.Span(data.Mode.ToString());
                        });
                        if (data.Reference is { } reference)
                        {
                            right.Item().Text(text =>
                            {
                                text.Span("Reference:  ").SemiBold();
                                text.Span(reference);
                            });
                        }
                    });
                });

                content.Item().PaddingTop(16).Background(Colors.Grey.Lighten4).Padding(14).Row(amount =>
                {
                    amount.RelativeItem().AlignMiddle().Text("Amount received")
                        .FontColor(Colors.Grey.Darken1);
                    amount.ConstantItem(180).AlignRight().Text(
                            "₹ " + data.Amount.ToString("N0", new CultureInfo("en-IN")))
                        .FontSize(22).Bold().FontColor(Blue);
                });

                content.Item().PaddingTop(10).AlignRight().Text(
                        "Balance after this payment: ₹ " +
                        data.BalanceAfter.ToString("N0", new CultureInfo("en-IN")))
                    .FontColor(data.BalanceAfter > 0 ? Colors.Orange.Darken2 : Colors.Green.Darken2)
                    .SemiBold();

                content.Item().PaddingTop(28).Row(signature =>
                {
                    signature.RelativeItem();
                    signature.ConstantItem(160).Column(sig =>
                    {
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        sig.Item().PaddingTop(4).AlignCenter().Text("Cashier / Accountant")
                            .FontColor(Colors.Grey.Darken1).FontSize(9);
                    });
                });
            });

            page.Footer().AlignCenter()
                .Text("Computer-generated receipt · Generated by SchoolErp")
                .FontColor(Colors.Grey.Medium).FontSize(8);
        })).GeneratePdf();
}
