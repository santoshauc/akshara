using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolErp.Application.Billing;
using SchoolErp.Domain.Billing;

namespace SchoolErp.Infrastructure.Reports;

/// <summary>Platform invoices to schools, A4 portrait (QuestPDF Community).</summary>
public sealed class QuestPdfInvoiceRenderer : IInvoiceRenderer
{
    private const string Teal = "#00695C";

    private static readonly CultureInfo Inr = new("en-IN");

    private static readonly string[] TableHeaders = ["Description", "Qty", "Unit (₹)", "Amount (₹)"];

    static QuestPdfInvoiceRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(InvoicePdfData data) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(style => style.FontSize(10.5f));

            page.Header().Column(header =>
            {
                header.Item().Row(top =>
                {
                    top.RelativeItem().Column(brand =>
                    {
                        brand.Item().Text("Akshara").FontSize(20).Bold().FontColor(Teal);
                        brand.Item().Text("School platform").FontColor(Colors.Grey.Darken1).FontSize(9);
                    });
                    top.ConstantItem(200).AlignRight().Column(invoice =>
                    {
                        // "TAX INVOICE" is a term of art under GST rules, not a
                        // flourish — a registered supplier's invoice must say it.
                        invoice.Item().AlignRight()
                            .Text(data.Tax is null ? "INVOICE" : "TAX INVOICE")
                            .Bold().LetterSpacing(0.15f);
                        invoice.Item().AlignRight().Text(data.InvoiceNumber).FontColor(Teal).SemiBold();
                        invoice.Item().AlignRight()
                            .Text($"Issued {data.IssuedOn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}")
                            .FontColor(Colors.Grey.Darken1).FontSize(9);
                        invoice.Item().AlignRight()
                            .Text($"Due {data.DueOn.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}")
                            .FontColor(Colors.Grey.Darken1).FontSize(9);
                    });
                });
                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Teal);
            });

            page.Content().PaddingVertical(18).Column(content =>
            {
                content.Item().Text(text =>
                {
                    text.Span("Billed to:  ").SemiBold();
                    text.Span(data.SchoolName);
                    if (data.SchoolCity is { } city)
                    {
                        text.Span($" · {city}").FontColor(Colors.Grey.Darken1);
                    }
                });

                if (data.Tax is { } tax)
                {
                    content.Item().PaddingTop(4).Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Grey.Darken1));
                        text.Span($"Supplier GSTIN {tax.SupplierGstin} · SAC {tax.SacCode}");
                        if (tax.BuyerGstin is { } buyerGstin)
                        {
                            text.Span($" · Recipient GSTIN {buyerGstin}");
                        }
                        if (tax.PlaceOfSupply is { } place)
                        {
                            text.Span($" · Place of supply: {place}");
                        }
                    });
                }

                if (data.Status != InvoiceStatus.Issued)
                {
                    content.Item().PaddingTop(6).Text(
                            data.Status == InvoiceStatus.Paid
                                ? $"PAID{(data.PaidOn is { } paid ? $" · {paid.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}" : "")}"
                                : "VOID")
                        .Bold()
                        .FontColor(data.Status == InvoiceStatus.Paid
                            ? Colors.Green.Darken2
                            : Colors.Red.Darken2);
                }

                content.Item().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(6);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(headerRow =>
                    {
                        foreach (var title in TableHeaders)
                        {
                            headerRow.Cell().Background(Colors.Grey.Lighten4).Padding(6)
                                .Text(title).SemiBold().FontSize(9.5f);
                        }
                    });

                    foreach (var line in data.Lines)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(6).Text(line.Description);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(6).AlignRight().Text(line.Quantity.ToString("N0", Inr));
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(6).AlignRight().Text(line.UnitAmount.ToString("N2", Inr));
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Padding(6).AlignRight().Text(line.Amount.ToString("N0", Inr));
                    }
                });

                // The GST breakup, above the grand total, in the shape the
                // school's accountant and a GST return both expect: taxable
                // value, then each levy on its own line at its own rate. Taxes
                // print with paise (they are rounded per component to 2 dp);
                // whole-rupee line amounts keep their existing N0 format.
                if (data.Tax is { } breakup)
                {
                    content.Item().PaddingTop(12).Row(taxRow =>
                    {
                        taxRow.RelativeItem();
                        taxRow.ConstantItem(240).Column(taxBox =>
                        {
                            TaxLine(taxBox, "Taxable value", breakup.TaxableAmount);
                            if (breakup.Igst > 0)
                            {
                                TaxLine(taxBox, $"IGST @ {breakup.RatePercent:0.#}%", breakup.Igst);
                            }
                            else
                            {
                                TaxLine(taxBox, $"CGST @ {breakup.RatePercent / 2:0.#}%", breakup.Cgst);
                                TaxLine(taxBox, $"SGST @ {breakup.RatePercent / 2:0.#}%", breakup.Sgst);
                            }
                        });
                    });
                }

                content.Item().PaddingTop(12).Row(total =>
                {
                    total.RelativeItem();
                    total.ConstantItem(240).Background(Colors.Grey.Lighten4).Padding(12).Row(box =>
                    {
                        box.RelativeItem().AlignMiddle().Text("Total").FontColor(Colors.Grey.Darken1);
                        box.ConstantItem(150).AlignRight()
                            .Text("₹ " + data.TotalAmount.ToString(
                                data.Tax is null ? "N0" : "N2", Inr))
                            .FontSize(18).Bold().FontColor(Teal);
                    });
                });

                if (data.Notes is { } notes)
                {
                    content.Item().PaddingTop(14).Text(text =>
                    {
                        text.Span("Notes:  ").SemiBold();
                        text.Span(notes).FontColor(Colors.Grey.Darken1);
                    });
                }
            });

            page.Footer().AlignCenter()
                .Text("Computer-generated invoice · Generated by Akshara")
                .FontColor(Colors.Grey.Medium).FontSize(8);
        })).GeneratePdf();

    private static void TaxLine(ColumnDescriptor column, string label, decimal amount) =>
        column.Item().PaddingBottom(2).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(9.5f).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(150).AlignRight().Text(amount.ToString("N2", Inr)).FontSize(9.5f);
        });
}
