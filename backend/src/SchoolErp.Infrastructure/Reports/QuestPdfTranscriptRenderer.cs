using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolErp.Application.Exams.Queries;

namespace SchoolErp.Infrastructure.Reports;

/// <summary>
/// Renders a consolidated grade sheet: school header, student block, one
/// table per semester with its SGPA, and the CGPA. QuestPDF Community licence,
/// as with the other renderers (docs/security-notes.md).
/// </summary>
public sealed class QuestPdfTranscriptRenderer : ITranscriptRenderer
{
    private const string Blue = "#1565C0";

    static QuestPdfTranscriptRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(TranscriptData data)
    {
        var sheet = data.Sheet;

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().Column(header =>
            {
                header.Item().AlignCenter().Text(data.SchoolName)
                    .FontSize(20).Bold().FontColor(Blue);
                if (!string.IsNullOrWhiteSpace(data.SchoolCity))
                {
                    header.Item().AlignCenter().Text(data.SchoolCity!)
                        .FontColor(Colors.Grey.Darken1);
                }

                header.Item().PaddingTop(6).AlignCenter().Text("CONSOLIDATED GRADE SHEET")
                    .FontSize(13).Bold().LetterSpacing(0.15f);
                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Blue);
            });

            page.Content().PaddingVertical(14).Column(content =>
            {
                content.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(sheet.StudentName).FontSize(13).Bold();
                        left.Item().Text(sheet.AdmissionNumber).FontColor(Colors.Grey.Darken2);
                        if (sheet.ProgrammeName is { } programme)
                        {
                            left.Item().Text(programme).FontColor(Colors.Grey.Darken2);
                        }
                    });

                    row.ConstantItem(150).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text($"CGPA {sheet.Cgpa:0.00}")
                            .FontSize(16).Bold().FontColor(Blue);
                        right.Item().AlignRight()
                            .Text($"{sheet.CreditsEarned} / {sheet.CreditsAttempted} credits")
                            .FontColor(Colors.Grey.Darken2);
                    });
                });

                foreach (var semester in sheet.Semesters)
                {
                    content.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem().Text(semester.CohortName).Bold();
                        row.ConstantItem(120).AlignRight()
                            // Invariant, like every other academic figure in these
                            // renderers: a transcript is a formal record, so "8.75"
                            // must not become "8,75" because of the server's locale.
                            .Text($"SGPA {(semester.Sgpa is { } s ? s.ToString("0.00", CultureInfo.InvariantCulture) : "—")}").Bold();
                    });
                    content.Item().Text($"{semester.ExamName} · {semester.EndDate:MMMM yyyy}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);

                    content.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(head =>
                        {
                            head.Cell().Element(HeaderCell).Text("Paper");
                            head.Cell().Element(HeaderCell).AlignCenter().Text("Credits");
                            head.Cell().Element(HeaderCell).AlignCenter().Text("Marks");
                            head.Cell().Element(HeaderCell).AlignCenter().Text("Grade");
                            head.Cell().Element(HeaderCell).AlignCenter().Text("Points");
                        });

                        foreach (var paper in semester.Papers)
                        {
                            table.Cell().Element(BodyCell).Text(paper.SubjectName);
                            table.Cell().Element(BodyCell).AlignCenter()
                                .Text(paper.Credits == 0
                                    ? "—"
                                    : paper.Credits.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(BodyCell).AlignCenter().Text(
                                paper.IsAbsent ? "Absent"
                                    : paper.Percent is { } pct ? $"{pct:0.#}%" : "—");
                            table.Cell().Element(BodyCell).AlignCenter().Text(paper.Grade).Bold();
                            table.Cell().Element(BodyCell).AlignCenter()
                                .Text(paper.GradePoint.ToString(CultureInfo.InvariantCulture));
                        }
                    });
                }

                // Says which ordinance produced these grades. A transcript that
                // does not is unverifiable by whoever receives it.
                content.Item().PaddingTop(18).Text(data.IsInstitutionDefinedScale
                        ? "Grades awarded under this institution's grading ordinance."
                        : "Grades awarded under the UGC recommended 10-point scale.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Darken1);
                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);
}
