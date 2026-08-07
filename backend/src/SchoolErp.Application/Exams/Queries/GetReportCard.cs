using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Exams.Queries;

/// <summary>Everything a report card shows, ready for rendering.</summary>
public sealed record ReportCardData(
    string SchoolName,
    string? SchoolCity,
    string StudentName,
    string AdmissionNumber,
    string? ClassName,
    string? SectionName,
    int? RollNumber,
    StudentResultDto Result);

/// <summary>Turns report-card data into a PDF. Implemented in Infrastructure.</summary>
public interface IReportCardRenderer
{
    byte[] Render(ReportCardData data);
}

/// <summary>
/// A student's report card for one exam as a PDF. Set
/// <paramref name="PublishedOnly"/> for parent-facing callers so drafts stay
/// invisible; staff may render drafts for proofing.
/// </summary>
public sealed record GetReportCardPdfQuery(
    Guid StudentId, Guid ExamId, bool PublishedOnly = false) : IRequest<byte[]>;

/// <summary>Composes result + student + school header, then renders.</summary>
public sealed class GetReportCardPdfQueryHandler : IRequestHandler<GetReportCardPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly ITenantContext _tenantContext;
    private readonly IReportCardRenderer _renderer;

    public GetReportCardPdfQueryHandler(
        IApplicationDbContext db,
        ISender sender,
        ITenantContext tenantContext,
        IReportCardRenderer renderer)
    {
        _db = db;
        _sender = sender;
        _tenantContext = tenantContext;
        _renderer = renderer;
    }

    public async Task<byte[]> Handle(GetReportCardPdfQuery request, CancellationToken cancellationToken)
    {
        var result = await _sender
            .Send(new GetStudentResultQuery(request.StudentId, request.ExamId), cancellationToken)
            .ConfigureAwait(false);
        if (request.PublishedOnly && result.ExamStatus != Domain.Exams.ExamStatus.Published)
        {
            throw new NotFoundException("Result (not published)", request.ExamId);
        }

        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == request.StudentId)
            .Select(s => new
            {
                Name = (s.FirstName + " " + s.LastName).Trim(),
                s.AdmissionNumber,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var placement = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == request.StudentId && e.AcademicYear!.IsCurrent)
            .Select(e => new
            {
                ClassName = e.SchoolClass!.Name,
                SectionName = e.Section != null ? e.Section.Name : null,
                e.RollNumber,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var school = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => new { t.Name, t.City })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        return _renderer.Render(new ReportCardData(
            school.Name,
            school.City,
            student.Name,
            student.AdmissionNumber,
            placement?.ClassName,
            placement?.SectionName,
            placement?.RollNumber,
            result));
    }
}
