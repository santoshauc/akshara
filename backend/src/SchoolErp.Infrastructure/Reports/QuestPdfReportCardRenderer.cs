using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolErp.Application.Exams.Queries;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Infrastructure.Reports;

/// <summary>
/// Renders report cards with QuestPDF (Community license — noted in
/// docs/security-notes.md). One A4 page: school header, student block,
/// per-subject marks table, totals and rank.
/// </summary>
public sealed class QuestPdfReportCardRenderer : IReportCardRenderer
{
    private const string Blue = "#1565C0";

    static QuestPdfReportCardRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ReportCardData data)
    {
        var result = data.Result;
        var template = data.Settings.Template;

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(11));

            page.Header().Column(header =>
            {
                header.Item().AlignCenter().Text(data.SchoolName)
                    .FontSize(20).Bold().FontColor(Blue);
                if (!string.IsNullOrWhiteSpace(data.SchoolCity))
                {
                    header.Item().AlignCenter().Text(data.SchoolCity!).FontColor(Colors.Grey.Darken1);
                }

                header.Item().PaddingTop(6).AlignCenter().Text("REPORT CARD")
                    .FontSize(13).Bold().LetterSpacing(0.15f);
                header.Item().AlignCenter().Text(result.ExamName).FontColor(Colors.Grey.Darken2);
                header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Blue);
            });

            page.Content().PaddingVertical(14).Column(content =>
            {
                content.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(text =>
                        {
                            text.Span("Student:  ").SemiBold();
                            text.Span(data.StudentName);
                        });
                        left.Item().Text(text =>
                        {
                            text.Span("Admission no:  ").SemiBold();
                            text.Span(data.AdmissionNumber);
                        });
                    });
                    row.RelativeItem().Column(right =>
                    {
                        right.Item().Text(text =>
                        {
                            text.Span("Class:  ").SemiBold();
                            text.Span($"{data.ClassName ?? "—"} {data.SectionName ?? ""}".Trim());
                        });
                        if (data.RollNumber is { } roll)
                        {
                            right.Item().Text(text =>
                            {
                                text.Span("Roll no:  ").SemiBold();
                                text.Span(roll.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            });
                        }
                    });
                });

                var showMarks = template != ReportCardTemplate.GradesOnly;
                var showGrade = template != ReportCardTemplate.MarksOnly;

                content.Item().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        if (showMarks)
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        }

                        if (showGrade)
                        {
                            columns.RelativeColumn(1);
                        }
                    });

                    table.Header(headerRow =>
                    {
                        static IContainer HeaderCell(IContainer cell) => cell
                            .Background(Blue).PaddingVertical(6).PaddingHorizontal(8);
                        headerRow.Cell().Element(HeaderCell).Text("Subject").Bold().FontColor(Colors.White);
                        if (showMarks)
                        {
                            headerRow.Cell().Element(HeaderCell).AlignRight().Text("Max marks").Bold().FontColor(Colors.White);
                            headerRow.Cell().Element(HeaderCell).AlignRight().Text("Obtained").Bold().FontColor(Colors.White);
                        }

                        if (showGrade)
                        {
                            headerRow.Cell().Element(HeaderCell).AlignRight().Text("Grade").Bold().FontColor(Colors.White);
                        }
                    });

                    var shade = false;
                    foreach (var line in result.Lines)
                    {
                        var background = shade ? Colors.Grey.Lighten4 : Colors.White;
                        shade = !shade;
                        IContainer BodyCell(IContainer cell) => cell
                            .Background(background).PaddingVertical(5).PaddingHorizontal(8);

                        table.Cell().Element(BodyCell).Text(line.SubjectName);
                        if (showMarks)
                        {
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text(line.MaxMarks.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text(line.IsAbsent
                                    ? "Absent"
                                    : (line.MarksObtained ?? 0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                        }

                        if (showGrade)
                        {
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text(line.IsAbsent ? "AB" : line.Grade).SemiBold();
                        }
                    }
                });

                content.Item().PaddingTop(14).Background(Colors.Grey.Lighten4).Padding(12).Row(summary =>
                {
                    summary.RelativeItem().Column(left =>
                    {
                        if (template == ReportCardTemplate.GradesOnly)
                        {
                            // Raw totals would leak the marks this template hides.
                            if (data.Attendance is { MarkedDays: > 0 } gradeOnlyDays)
                            {
                                left.Item().Text(text =>
                                {
                                    text.Span("Attendance:  ").SemiBold();
                                    text.Span(string.Create(
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        $"{gradeOnlyDays.PresentDays} of {gradeOnlyDays.MarkedDays} days"));
                                });
                            }

                            return;
                        }

                        var totals = string.Create(
                            System.Globalization.CultureInfo.InvariantCulture,
                            $"{result.TotalObtained:0.##} / {result.TotalMax:0.##}   ({result.Percent:0.#}%)");
                        left.Item().Text(text =>
                        {
                            text.Span("Total:  ").SemiBold();
                            text.Span(totals);
                        });
                        if (data.Attendance is { MarkedDays: > 0 } days)
                        {
                            left.Item().Text(text =>
                            {
                                text.Span("Attendance:  ").SemiBold();
                                text.Span(string.Create(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    $"{days.PresentDays} of {days.MarkedDays} days"));
                            });
                        }

                        if (result.SectionRank is { } rank)
                        {
                            var rankText = string.Create(
                                System.Globalization.CultureInfo.InvariantCulture,
                                $"{rank} of {result.SectionSize}");
                            left.Item().Text(text =>
                            {
                                text.Span("Section rank:  ").SemiBold();
                                text.Span(rankText);
                            });
                        }
                    });
                    summary.ConstantItem(140).AlignRight().Column(right =>
                    {
                        right.Item().AlignRight().Text("Overall grade").FontColor(Colors.Grey.Darken1);
                        right.Item().AlignRight().Text(result.OverallGrade)
                            .FontSize(26).Bold().FontColor(Blue);
                    });
                });

                if (data.Settings.ShowRemarks)
                {
                    content.Item().PaddingTop(16).Column(remarks =>
                    {
                        remarks.Item().Text("Class teacher's remarks").SemiBold();
                        // Two ruled lines to write on — deliberately blank.
                        remarks.Item().PaddingTop(14).LineHorizontal(1)
                            .LineColor(Colors.Grey.Medium);
                        remarks.Item().PaddingTop(18).LineHorizontal(1)
                            .LineColor(Colors.Grey.Medium);
                    });
                }

                content.Item().PaddingTop(30).Row(signatures =>
                {
                    static IContainer SignatureCell(IContainer cell) => cell.PaddingHorizontal(12);
                    foreach (var signatory in data.Settings.Signatories)
                    {
                        signatures.RelativeItem().Element(SignatureCell).Column(sig =>
                        {
                            sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                            sig.Item().PaddingTop(4).AlignCenter()
                                .Text(signatory).FontColor(Colors.Grey.Darken1);
                        });
                    }
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Generated by Akshara · ").FontColor(Colors.Grey.Medium).FontSize(9);
                text.Span(DateTime.UtcNow.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture))
                    .FontColor(Colors.Grey.Medium).FontSize(9);
            });
        })).GeneratePdf();
    }
}
