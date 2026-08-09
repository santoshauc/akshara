using ClosedXML.Excel;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Queries;

namespace SchoolErp.Infrastructure.Files;

/// <summary>The student-list export sheet (ClosedXML).</summary>
public sealed class StudentListWorkbook : IStudentListWorkbook
{
    public byte[] Build(string title, IReadOnlyList<StudentListItemDto> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Students");

        string[] headers =
            ["Admission no.", "First name", "Last name", "Gender", "Class", "Section", "Roll", "Status"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3ECF7");
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            sheet.Cell(2 + r, 1).Value = row.AdmissionNumber;
            sheet.Cell(2 + r, 2).Value = row.FirstName;
            sheet.Cell(2 + r, 3).Value = row.LastName;
            sheet.Cell(2 + r, 4).Value = row.Gender.ToString();
            sheet.Cell(2 + r, 5).Value = row.ClassName ?? "";
            sheet.Cell(2 + r, 6).Value = row.SectionName ?? "";
            sheet.Cell(2 + r, 7).Value = row.RollNumber?.ToString() ?? "";
            sheet.Cell(2 + r, 8).Value = row.Status.ToString();
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, headers.Length).AdjustToContents();
        sheet.Cell(rows.Count + 3, 1).Value = title;
        sheet.Cell(rows.Count + 3, 1).Style.Font.Italic = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
