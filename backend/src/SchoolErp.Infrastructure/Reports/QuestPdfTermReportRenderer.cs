using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolErp.Application.Exams;

namespace SchoolErp.Infrastructure.Reports;

/// <summary>
/// Renders the weighted term/annual report card with QuestPDF (Community
/// license — noted in docs/security-notes.md): per-subject percentages per
/// component exam, weighted totals, co-scholastic grades and remarks.
/// </summary>
public sealed class QuestPdfTermReportRenderer : ITermReportRenderer
{
    private const string Blue = "#1565C0";

    static QuestPdfTermReportRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(TermReportCardData data) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10.5f));

            page.Header().Column(header =>
            {
                header.Item().AlignCenter().Text(data.SchoolName)
                    .FontSize(20).Bold().FontColor(Blue);
                if (!string.IsNullOrWhiteSpace(data.SchoolCity))
                {
                    header.Item().AlignCenter().Text(data.SchoolCity!)
                        .FontColor(Colors.Grey.Darken1);
                }

                header.Item().PaddingTop(6).AlignCenter().Text(data.TermName.ToUpperInvariant())
                    .FontSize(13).Bold().LetterSpacing(0.15f);
                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Blue);
            });

            page.Content().PaddingVertical(14).Column(content =>
            {
                content.Item().Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Student:  ").SemiBold();
                        text.Span(data.StudentName);
                    });
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Admission no:  ").SemiBold();
                        text.Span(data.AdmissionNumber);
                    });
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Class:  ").SemiBold();
                        text.Span(data.ClassName ?? "—");
                    });
                });

                content.Item().PaddingTop(14).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        foreach (var _ in data.Components)
                        {
                            columns.RelativeColumn(2);
                        }

                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                    });

                    table.Header(headerRow =>
                    {
                        static IContainer HeaderCell(IContainer cell) => cell
                            .Background(Blue).PaddingVertical(6).PaddingHorizontal(6);
                        headerRow.Cell().Element(HeaderCell).Text("Subject")
                            .Bold().FontColor(Colors.White);
                        foreach (var component in data.Components)
                        {
                            headerRow.Cell().Element(HeaderCell).AlignRight()
                                .Text($"{component.ExamName}\n({component.WeightPercent:0.#}%)")
                                .Bold().FontColor(Colors.White).FontSize(8.5f);
                        }

                        headerRow.Cell().Element(HeaderCell).AlignRight().Text("Weighted %")
                            .Bold().FontColor(Colors.White);
                        headerRow.Cell().Element(HeaderCell).AlignRight().Text("Grade")
                            .Bold().FontColor(Colors.White);
                    });

                    var shade = false;
                    foreach (var subject in data.Subjects)
                    {
                        var background = shade ? Colors.Grey.Lighten4 : Colors.White;
                        shade = !shade;
                        IContainer BodyCell(IContainer cell) => cell
                            .Background(background).PaddingVertical(5).PaddingHorizontal(6);

                        table.Cell().Element(BodyCell).Text(subject.SubjectName);
                        foreach (var percent in subject.PercentByComponent)
                        {
                            table.Cell().Element(BodyCell).AlignRight().Text(
                                percent is { } p
                                    ? p.ToString("0.#", CultureInfo.InvariantCulture)
                                    : "—");
                        }

                        table.Cell().Element(BodyCell).AlignRight()
                            .Text(subject.WeightedPercent.ToString("0.#", CultureInfo.InvariantCulture))
                            .SemiBold();
                        table.Cell().Element(BodyCell).AlignRight().Text(subject.Grade).SemiBold();
                    }
                });

                content.Item().PaddingTop(12).Background(Colors.Grey.Lighten4).Padding(12).Row(summary =>
                {
                    summary.RelativeItem().AlignMiddle().Text(text =>
                    {
                        text.Span("Overall weighted result:  ").SemiBold();
                        text.Span(data.OverallPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                    });
                    summary.ConstantItem(120).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text("Final grade").FontColor(Colors.Grey.Darken1);
                        right.Item().AlignRight().Text(data.OverallGrade)
                            .FontSize(24).Bold().FontColor(Blue);
                    });
                });

                if (data.CoScholastic.Count > 0)
                {
                    content.Item().PaddingTop(14).Text("Co-scholastic areas").Bold();
                    content.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                        });
                        foreach (var (area, grade) in data.CoScholastic.OrderBy(a => a.Key))
                        {
                            table.Cell().PaddingVertical(3).PaddingHorizontal(6).Text(area);
                            table.Cell().PaddingVertical(3).PaddingHorizontal(6)
                                .AlignRight().Text(grade).SemiBold();
                        }
                    });
                }

                if (!string.IsNullOrWhiteSpace(data.Remarks))
                {
                    content.Item().PaddingTop(14).Text("Class teacher's remarks").Bold();
                    content.Item().PaddingTop(4).Background(Colors.Grey.Lighten5)
                        .Padding(10).Text(data.Remarks!).Italic();
                }

                content.Item().PaddingTop(30).Row(signatures =>
                {
                    static IContainer SignatureCell(IContainer cell) => cell.PaddingHorizontal(12);
                    signatures.RelativeItem().Element(SignatureCell).Column(sig =>
                    {
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        sig.Item().PaddingTop(4).AlignCenter().Text("Class teacher")
                            .FontColor(Colors.Grey.Darken1);
                    });
                    signatures.RelativeItem().Element(SignatureCell).Column(sig =>
                    {
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        sig.Item().PaddingTop(4).AlignCenter().Text("Principal")
                            .FontColor(Colors.Grey.Darken1);
                    });
                    signatures.RelativeItem().Element(SignatureCell).Column(sig =>
                    {
                        sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        sig.Item().PaddingTop(4).AlignCenter().Text("Parent / Guardian")
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            page.Footer().AlignCenter()
                .Text("Generated by Akshara")
                .FontColor(Colors.Grey.Medium).FontSize(9);
        })).GeneratePdf();
}
