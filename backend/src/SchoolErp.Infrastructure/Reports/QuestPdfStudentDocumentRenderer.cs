using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolErp.Application.Students.Queries;
using SchoolErp.Domain.Students;

namespace SchoolErp.Infrastructure.Reports;

/// <summary>
/// Renders official student documents with QuestPDF (Community license —
/// noted in docs/security-notes.md): Transfer Certificate and bonafide on A4,
/// the ID card on a CR80-ish card page.
/// </summary>
public sealed class QuestPdfStudentDocumentRenderer : IStudentDocumentRenderer
{
    private const string Blue = "#1565C0";

    static QuestPdfStudentDocumentRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(StudentDocumentType type, StudentDocumentData data) => type switch
    {
        StudentDocumentType.TransferCertificate => Certificate(data, "TRANSFER CERTIFICATE", TcBody(data)),
        StudentDocumentType.BonafideCertificate => Certificate(data, "BONAFIDE CERTIFICATE", BonafideBody(data)),
        StudentDocumentType.IdCard => IdCard(data),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown document type."),
    };

    private static string Pronoun(Gender gender, bool possessive) => gender switch
    {
        Gender.Male => possessive ? "his" : "he",
        Gender.Female => possessive ? "her" : "she",
        _ => possessive ? "their" : "they",
    };

    private static string D(DateOnly date) =>
        date.ToString("dd MMMM yyyy", CultureInfo.InvariantCulture);

    private static string TcBody(StudentDocumentData data) =>
        $"This is to certify that {data.StudentName} " +
        $"(Admission No. {data.AdmissionNumber}), born on {D(data.DateOfBirth)}, " +
        $"was a bonafide student of this school from {D(data.AdmissionDate)} " +
        $"to {D(data.IssuedOn)}. At the time of leaving, " +
        $"{Pronoun(data.Gender, false)} was studying in " +
        $"{data.ClassName ?? "—"} {data.SectionName ?? ""}".Trim() +
        $" ({data.AcademicYearName ?? "current session"}). " +
        $"{char.ToUpperInvariant(Pronoun(data.Gender, true)[0])}{Pronoun(data.Gender, true)[1..]} " +
        "conduct and character during the period of study were found to be good. " +
        "All dues to the school have been cleared and " +
        $"{Pronoun(data.Gender, false)} is free to seek admission elsewhere.";

    private static string BonafideBody(StudentDocumentData data) =>
        $"This is to certify that {data.StudentName} " +
        $"(Admission No. {data.AdmissionNumber}), born on {D(data.DateOfBirth)}, " +
        $"is a bonafide student of this school, presently studying in " +
        $"{data.ClassName ?? "—"} {data.SectionName ?? ""}".Trim() +
        $" during the academic session {data.AcademicYearName ?? "—"}. " +
        (data.GuardianName is { } guardianName
            ? $"{char.ToUpperInvariant(Pronoun(data.Gender, false)[0])}{Pronoun(data.Gender, false)[1..]} " +
              $"is the ward of {guardianName}. "
            : "") +
        "This certificate is issued on request of the parent/guardian for " +
        "whatever legitimate purpose it may serve.";

    private static byte[] Certificate(StudentDocumentData data, string title, string body) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(48);
            page.DefaultTextStyle(style => style.FontSize(12));

            page.Header().Column(header =>
            {
                header.Item().AlignCenter().Text(data.SchoolName)
                    .FontSize(22).Bold().FontColor(Blue);
                if (data.SchoolAddress is { } schoolAddress)
                {
                    header.Item().AlignCenter().Text(schoolAddress)
                        .FontColor(Colors.Grey.Darken1);
                }
                if (data.AffiliationBoard is { } board)
                {
                    header.Item().AlignCenter()
                        .Text($"Affiliated to {board}" +
                            (data.AffiliationNumber is { } number
                                ? $" · Affiliation No. {number}" : ""))
                        .FontSize(10).FontColor(Colors.Grey.Darken1);
                }

                header.Item().PaddingTop(18).AlignCenter().Text(title)
                    .FontSize(15).Bold().LetterSpacing(0.2f);
                header.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Blue);
            });

            page.Content().PaddingVertical(24).Column(content =>
            {
                content.Item().Row(reference =>
                {
                    reference.RelativeItem().Text(
                        $"Ref: {data.AdmissionNumber}/{data.IssuedOn.Year}")
                        .FontColor(Colors.Grey.Darken1);
                    reference.RelativeItem().AlignRight().Text($"Date: {D(data.IssuedOn)}")
                        .FontColor(Colors.Grey.Darken1);
                });

                content.Item().PaddingTop(28).Text(body).LineHeight(1.8f).Justify();

                content.Item().PaddingTop(90).Row(signatures =>
                {
                    signatures.RelativeItem().Column(sig =>
                    {
                        sig.Item().Width(160).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        sig.Item().PaddingTop(4).Text("Class teacher")
                            .FontColor(Colors.Grey.Darken1);
                    });
                    signatures.RelativeItem().AlignRight().Column(sig =>
                    {
                        sig.Item().AlignRight().Width(160).LineHorizontal(1)
                            .LineColor(Colors.Grey.Medium);
                        sig.Item().PaddingTop(4).AlignRight().Text("Principal (seal)")
                            .FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Generated by SchoolErp · ").FontColor(Colors.Grey.Medium).FontSize(9);
                text.Span(D(data.IssuedOn)).FontColor(Colors.Grey.Medium).FontSize(9);
            });
        })).GeneratePdf();

    private static byte[] IdCard(StudentDocumentData data) =>
        Document.Create(document => document.Page(page =>
        {
            // CR80 card ratio scaled up for print-and-cut (86 x 54 mm ≈ 244 x 153 pt).
            page.Size(244, 153);
            page.Margin(0);
            page.DefaultTextStyle(style => style.FontSize(8));

            page.Content().Column(card =>
            {
                card.Item().Background(Blue).PaddingVertical(7).PaddingHorizontal(10)
                    .Column(header =>
                    {
                        header.Item().Text(data.SchoolName).Bold()
                            .FontSize(10).FontColor(Colors.White);
                        header.Item().Text("STUDENT IDENTITY CARD")
                            .FontSize(6.5f).FontColor(Colors.Blue.Lighten4);
                    });

                card.Item().Padding(10).Row(body =>
                {
                    body.ConstantItem(56).Column(photoColumn =>
                    {
                        if (data.PhotoBytes is { } photo)
                        {
                            photoColumn.Item().Height(64).Image(photo).FitArea();
                        }
                        else
                        {
                            photoColumn.Item().Height(64).Background(Colors.Grey.Lighten3)
                                .AlignCenter().AlignMiddle().Text("PHOTO")
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        }
                    });

                    body.RelativeItem().PaddingLeft(10).Column(details =>
                    {
                        details.Item().Text(data.StudentName).Bold().FontSize(10.5f);
                        details.Spacing(2);
                        details.Item().Text(
                            $"Class: {data.ClassName ?? "—"} {data.SectionName ?? ""}".Trim() +
                            (data.RollNumber is { } roll ? $" · Roll {roll}" : ""));
                        details.Item().Text($"Adm. No: {data.AdmissionNumber}");
                        details.Item().Text(
                            $"DOB: {data.DateOfBirth.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}");
                        if (data.GuardianName is { } guardianName)
                        {
                            details.Item().Text($"Guardian: {guardianName}");
                        }
                        if (data.GuardianPhone is { } guardianPhone)
                        {
                            details.Item().Text($"Contact: {guardianPhone}");
                        }
                    });
                });

                card.Item().AlignBottom().Background(Colors.Grey.Lighten4)
                    .PaddingVertical(4).PaddingHorizontal(10).Row(footer =>
                    {
                        footer.RelativeItem().Text(data.AcademicYearName ?? "")
                            .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                        footer.RelativeItem().AlignRight().Text("Principal")
                            .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                    });
            });
        })).GeneratePdf();
}
