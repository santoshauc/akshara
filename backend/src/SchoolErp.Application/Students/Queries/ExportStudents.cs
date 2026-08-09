using MediatR;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Students.Queries;

/// <summary>Builds the student-list export workbook. Implemented in Infrastructure.</summary>
public interface IStudentListWorkbook
{
    byte[] Build(string title, IReadOnlyList<StudentListItemDto> rows);
}

/// <summary>
/// The current student list as an Excel file, honouring the same filters and
/// sort as the grid.
/// </summary>
public sealed record ExportStudentsQuery(
    string? Search,
    Guid? AcademicYearId,
    Guid? SchoolClassId,
    Guid? SectionId,
    StudentStatus? Status,
    string? SortBy,
    bool SortDescending) : IRequest<byte[]>;

/// <summary>Pages through the grid query so filter/sort logic lives once.</summary>
public sealed class ExportStudentsQueryHandler : IRequestHandler<ExportStudentsQuery, byte[]>
{
    private const int PageSize = 100;
    private const int MaxRows = 5_000;

    private readonly ISender _sender;
    private readonly IStudentListWorkbook _workbook;

    public ExportStudentsQueryHandler(ISender sender, IStudentListWorkbook workbook)
    {
        _sender = sender;
        _workbook = workbook;
    }

    public async Task<byte[]> Handle(ExportStudentsQuery request, CancellationToken cancellationToken)
    {
        var rows = new List<StudentListItemDto>();
        for (var page = 1; rows.Count < MaxRows; page++)
        {
            var result = await _sender.Send(
                new GetStudentsQuery(
                    request.Search, request.AcademicYearId, request.SchoolClassId,
                    request.SectionId, request.Status, page, PageSize,
                    request.SortBy, request.SortDescending),
                cancellationToken).ConfigureAwait(false);
            rows.AddRange(result.Items);
            if (page * PageSize >= result.TotalCount)
            {
                break;
            }
        }

        return _workbook.Build($"Students — exported {DateTime.UtcNow:yyyy-MM-dd}", rows);
    }
}
