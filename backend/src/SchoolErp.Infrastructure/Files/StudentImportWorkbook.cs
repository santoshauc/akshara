using ClosedXML.Excel;
using FluentValidation;
using SchoolErp.Application.Students.Commands;

namespace SchoolErp.Infrastructure.Files;

/// <summary>
/// The student-import workbook, built and read with ClosedXML. Column order
/// is the contract between <see cref="BuildTemplate"/> and
/// <see cref="Parse"/> — change both together.
/// </summary>
public sealed class StudentImportWorkbook : IStudentImportWorkbook
{
    private const string SheetName = "Students";

    private static readonly string[] Headers =
    [
        "Admission number (blank = auto)",
        "First name *",
        "Last name *",
        "Date of birth (YYYY-MM-DD) *",
        "Gender (Male/Female/Other) *",
        "Class *",
        "Section *",
        "Roll number",
        "Admission date (YYYY-MM-DD, blank = today)",
        "Blood group",
        "City",
        "State",
        "Guardian first name *",
        "Guardian last name *",
        "Guardian relation (Father/Mother/Guardian/Other) *",
        "Guardian phone (+91…) *",
        "Guardian email",
    ];

    public byte[] BuildTemplate(ImportTemplateContext context)
    {
        using var workbook = new XLWorkbook();

        var sheet = workbook.Worksheets.Add(SheetName);
        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3ECF7");
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, Headers.Length).Width = 24;
        // Keep date and phone cells as text so Excel doesn't reformat them.
        sheet.Columns(4, 4).Style.NumberFormat.Format = "@";
        sheet.Columns(9, 9).Style.NumberFormat.Format = "@";
        sheet.Columns(16, 16).Style.NumberFormat.Format = "@";

        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = $"Student import — {context.SchoolName}";
        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(1, 1).Style.Font.FontSize = 14;

        var lines = new[]
        {
            "",
            "1. Fill one student per row on the 'Students' sheet. Columns marked * are required.",
            "2. Dates are YYYY-MM-DD, e.g. 2016-04-12. Format the cell as Text if Excel changes it.",
            "3. Class and Section must match the school's setup exactly (list below).",
            "4. The guardian's phone becomes their parent-app login. Siblings entered with the",
            "   same phone are linked to one guardian automatically.",
            "5. Leave 'Admission number' blank to auto-generate the next number.",
            "6. The file is imported all-or-nothing: if any row has a problem, nothing is",
            "   imported and every problem is listed with its row number. Fix and re-upload.",
            "",
            "Example row:",
        };
        for (var i = 0; i < lines.Length; i++)
        {
            instructions.Cell(2 + i, 1).Value = lines[i];
        }

        var exampleRow = 2 + lines.Length;
        var example = new[]
        {
            "", "Ananya", "Sharma", "2016-04-12", "Female",
            context.Classes.Count > 0 ? context.Classes[0].ClassName : "Grade 5",
            context.Classes.Count > 0 && context.Classes[0].Sections.Count > 0
                ? context.Classes[0].Sections[0] : "A",
            "12", "", "B+", "Hyderabad", "Telangana",
            "Priya", "Sharma", "Mother", "+919876543210", "priya@example.com",
        };
        for (var i = 0; i < example.Length; i++)
        {
            var cell = instructions.Cell(exampleRow, 1 + i);
            cell.Value = example[i];
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FA");
        }

        var classesRow = exampleRow + 2;
        instructions.Cell(classesRow, 1).Value = "Classes and sections in this school:";
        instructions.Cell(classesRow, 1).Style.Font.Bold = true;
        for (var i = 0; i < context.Classes.Count; i++)
        {
            var (className, sections) = context.Classes[i];
            instructions.Cell(classesRow + 1 + i, 1).Value = className;
            instructions.Cell(classesRow + 1 + i, 2).Value =
                $"Sections: {string.Join(", ", sections)}";
        }

        instructions.Column(1).Width = 90;
        instructions.Column(2).Width = 40;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public IReadOnlyList<StudentImportRow> Parse(byte[] content)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(content));
        }
        catch (Exception)
        {
            throw new ValidationException(
                "The file could not be read as an Excel workbook (.xlsx). " +
                "Download the template, fill it, and upload that file.");
        }

        using (workbook)
        {
            if (!workbook.TryGetWorksheet(SheetName, out var sheet))
            {
                throw new ValidationException(
                    $"The workbook has no '{SheetName}' sheet — " +
                    "start from the downloaded template.");
            }

            var rows = new List<StudentImportRow>();
            foreach (var row in sheet.RowsUsed().Skip(1)) // header
            {
                if (row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString())))
                {
                    continue;
                }

                rows.Add(new StudentImportRow(
                    row.RowNumber(),
                    Text(row, 1), Text(row, 2), Text(row, 3),
                    DateText(row, 4), Text(row, 5), Text(row, 6), Text(row, 7),
                    Text(row, 8), DateText(row, 9), Text(row, 10), Text(row, 11),
                    Text(row, 12), Text(row, 13), Text(row, 14), Text(row, 15),
                    Text(row, 16), Text(row, 17)));
            }

            return rows;
        }
    }

    private static string? Text(IXLRow row, int column)
    {
        var value = row.Cell(column).GetString().Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>Accepts both text dates and real Excel date cells.</summary>
    private static string? DateText(IXLRow row, int column)
    {
        var cell = row.Cell(column);
        if (cell.DataType == XLDataType.DateTime)
        {
            return cell.GetDateTime().ToString(
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return Text(row, column);
    }
}
