using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Exams.Queries;
using SchoolErp.Domain.Exams;

namespace SchoolErp.Application.Exams;

/// <summary>A term report definition as listed to staff.</summary>
public sealed record TermReportDto(
    Guid Id,
    string Name,
    Guid AcademicYearId,
    IReadOnlyList<TermReportComponentDto> Components);

/// <summary>One weighted exam inside a definition.</summary>
public sealed record TermReportComponentDto(Guid ExamId, string ExamName, decimal WeightPercent);

/// <summary>Weighted line of the final card: one subject across all exams.</summary>
public sealed record TermSubjectLineDto(
    string SubjectName,
    IReadOnlyList<decimal?> PercentByComponent,
    decimal WeightedPercent,
    string Grade);

/// <summary>Everything the term report card PDF shows.</summary>
public sealed record TermReportCardData(
    string SchoolName,
    string? SchoolCity,
    string TermName,
    string StudentName,
    string AdmissionNumber,
    string? ClassName,
    IReadOnlyList<TermReportComponentDto> Components,
    IReadOnlyList<TermSubjectLineDto> Subjects,
    decimal OverallPercent,
    string OverallGrade,
    IReadOnlyDictionary<string, string> CoScholastic,
    string? Remarks);

/// <summary>Turns term report data into a PDF. Implemented in Infrastructure.</summary>
public interface ITermReportRenderer
{
    byte[] Render(TermReportCardData data);
}

/// <summary>Creates a term report from weighted exams (weights sum to 100).</summary>
public sealed record CreateTermReportCommand(
    Guid AcademicYearId,
    string Name,
    IReadOnlyList<(Guid ExamId, decimal WeightPercent)> Components) : IRequest<Guid>;

/// <summary>Definition shape rules.</summary>
public sealed class CreateTermReportCommandValidator : AbstractValidator<CreateTermReportCommand>
{
    public CreateTermReportCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Components).NotEmpty()
            .Must(c => c.Select(x => x.ExamId).Distinct().Count() == c.Count)
            .WithMessage("Each exam may appear only once.")
            .Must(c => Math.Abs(c.Sum(x => x.WeightPercent) - 100m) < 0.01m)
            .WithMessage("Component weights must sum to 100.");
    }
}

/// <summary>Creates the definition after validating the exams.</summary>
public sealed class CreateTermReportCommandHandler : IRequestHandler<CreateTermReportCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateTermReportCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateTermReportCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.TermReports.AnyAsync(
                t => t.AcademicYearId == request.AcademicYearId && t.Name == name,
                cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"Term report '{name}' already exists for this year.");
        }

        var examIds = request.Components.Select(c => c.ExamId).ToList();
        var known = await _db.Exams
            .Where(e => examIds.Contains(e.Id) && e.AcademicYearId == request.AcademicYearId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var missing = examIds.Except(known).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException("Exam (in this year)", missing[0]);
        }

        var report = new TermReport
        {
            AcademicYearId = request.AcademicYearId,
            Name = name,
            Components = request.Components.Select(c => new TermReportComponent
            {
                ExamId = c.ExamId,
                WeightPercent = c.WeightPercent,
            }).ToList(),
        };
        _db.TermReports.Add(report);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return report.Id;
    }
}

/// <summary>Upserts a student's co-scholastic grades and remarks.</summary>
public sealed record SetTermStudentInputCommand(
    Guid TermReportId,
    Guid StudentId,
    IReadOnlyDictionary<string, string> CoScholastic,
    string? Remarks) : IRequest;

/// <summary>Input shape rules.</summary>
public sealed class SetTermStudentInputCommandValidator
    : AbstractValidator<SetTermStudentInputCommand>
{
    public SetTermStudentInputCommandValidator()
    {
        RuleFor(c => c.Remarks).MaximumLength(1024);
        RuleFor(c => c.CoScholastic)
            .Must(c => c.Count <= 12).WithMessage("At most 12 co-scholastic areas.")
            .Must(c => c.All(kv => kv.Key.Length <= 64 && kv.Value.Length <= 8))
            .WithMessage("Area names up to 64 chars; grades up to 8.");
    }
}

/// <summary>Upsert handler.</summary>
public sealed class SetTermStudentInputCommandHandler : IRequestHandler<SetTermStudentInputCommand>
{
    private readonly IApplicationDbContext _db;

    public SetTermStudentInputCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetTermStudentInputCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.TermReports.AnyAsync(t => t.Id == request.TermReportId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(TermReport), request.TermReportId);
        }

        var input = await _db.TermStudentInputs
            .FirstOrDefaultAsync(
                t => t.TermReportId == request.TermReportId && t.StudentId == request.StudentId,
                cancellationToken)
            .ConfigureAwait(false);
        if (input is null)
        {
            input = new TermStudentInput
            {
                TermReportId = request.TermReportId,
                StudentId = request.StudentId,
            };
            _db.TermStudentInputs.Add(input);
        }

        input.CoScholasticJson = request.CoScholastic.Count == 0
            ? null
            : JsonSerializer.Serialize(request.CoScholastic);
        input.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Term reports of a year, components included.</summary>
public sealed record GetTermReportsQuery(Guid AcademicYearId)
    : IRequest<IReadOnlyList<TermReportDto>>;

/// <summary>Simple projection.</summary>
public sealed class GetTermReportsQueryHandler
    : IRequestHandler<GetTermReportsQuery, IReadOnlyList<TermReportDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTermReportsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TermReportDto>> Handle(
        GetTermReportsQuery request, CancellationToken cancellationToken) =>
        await _db.TermReports.AsNoTracking()
            .Where(t => t.AcademicYearId == request.AcademicYearId)
            .OrderBy(t => t.Name)
            .Select(t => new TermReportDto(
                t.Id, t.Name, t.AcademicYearId,
                t.Components
                    .OrderBy(c => c.Exam!.Name)
                    .Select(c => new TermReportComponentDto(
                        c.ExamId, c.Exam!.Name, c.WeightPercent))
                    .ToList()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>
/// The final weighted report card as a PDF. With <paramref name="PublishedOnly"/>
/// (parents), every component exam must be published.
/// </summary>
public sealed record GetTermReportCardPdfQuery(
    Guid TermReportId, Guid StudentId, bool PublishedOnly = false) : IRequest<byte[]>;

/// <summary>Aggregates component results, weights them, renders.</summary>
public sealed class GetTermReportCardPdfQueryHandler
    : IRequestHandler<GetTermReportCardPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly ITenantContext _tenantContext;
    private readonly ITermReportRenderer _renderer;

    public GetTermReportCardPdfQueryHandler(
        IApplicationDbContext db,
        ISender sender,
        ITenantContext tenantContext,
        ITermReportRenderer renderer)
    {
        _db = db;
        _sender = sender;
        _tenantContext = tenantContext;
        _renderer = renderer;
    }

    public async Task<byte[]> Handle(
        GetTermReportCardPdfQuery request, CancellationToken cancellationToken)
    {
        var report = await _db.TermReports.AsNoTracking()
            .Where(t => t.Id == request.TermReportId)
            .Select(t => new
            {
                t.Name,
                Components = t.Components
                    .OrderBy(c => c.Exam!.Name)
                    .Select(c => new TermReportComponentDto(
                        c.ExamId, c.Exam!.Name, c.WeightPercent))
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(TermReport), request.TermReportId);

        // One result per component through the existing per-exam pipeline.
        var results = new List<(TermReportComponentDto Component, StudentResultDto Result)>();
        foreach (var component in report.Components)
        {
            var result = await _sender
                .Send(new GetStudentResultQuery(request.StudentId, component.ExamId),
                    cancellationToken)
                .ConfigureAwait(false);
            if (request.PublishedOnly && result.ExamStatus != ExamStatus.Published)
            {
                throw new NotFoundException("Term report (component not published)", component.ExamId);
            }

            results.Add((component, result));
        }

        // Subject rows: per-component percent, then the weighted total. A
        // subject missing from a component contributes nothing there and its
        // weight is renormalized over the components that do carry it.
        var subjectNames = results
            .SelectMany(r => r.Result.Lines.Select(l => l.SubjectName))
            .Distinct()
            .OrderBy(n => n)
            .ToList();
        var subjects = new List<TermSubjectLineDto>();
        foreach (var subject in subjectNames)
        {
            var percents = new List<decimal?>();
            decimal weighted = 0, weightCovered = 0;
            foreach (var (component, result) in results)
            {
                var line = result.Lines.FirstOrDefault(l => l.SubjectName == subject);
                if (line is null || line.MaxMarks == 0)
                {
                    percents.Add(null);
                    continue;
                }

                var percent = Math.Round(
                    (line.IsAbsent ? 0 : line.MarksObtained ?? 0) * 100m / line.MaxMarks, 1);
                percents.Add(percent);
                weighted += percent * component.WeightPercent;
                weightCovered += component.WeightPercent;
            }

            var weightedPercent = weightCovered == 0
                ? 0
                : Math.Round(weighted / weightCovered, 1);
            subjects.Add(new TermSubjectLineDto(
                subject, percents, weightedPercent, GradeFor(weightedPercent)));
        }

        var overall = subjects.Count == 0
            ? 0
            : Math.Round(subjects.Average(s => s.WeightedPercent), 1);

        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == request.StudentId)
            .Select(s => new
            {
                Name = (s.FirstName + " " + s.LastName).Trim(),
                s.AdmissionNumber,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Student", request.StudentId);

        var className = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == request.StudentId && e.AcademicYear!.IsCurrent)
            .Select(e => e.SchoolClass!.Name + (e.Section != null ? " " + e.Section.Name : ""))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var school = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => new { t.Name, t.City })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var input = await _db.TermStudentInputs.AsNoTracking()
            .Where(t => t.TermReportId == request.TermReportId && t.StudentId == request.StudentId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var coScholastic = input?.CoScholasticJson is { } json
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []
            : [];

        return _renderer.Render(new TermReportCardData(
            school.Name,
            school.City,
            report.Name,
            student.Name,
            student.AdmissionNumber,
            className,
            report.Components,
            subjects,
            overall,
            GradeFor(overall),
            coScholastic,
            input?.Remarks));
    }

    /// <summary>Same band edges the per-exam pipeline uses.</summary>
    private static string GradeFor(decimal percent) => percent switch
    {
        >= 91 => "A1",
        >= 81 => "A2",
        >= 71 => "B1",
        >= 61 => "B2",
        >= 51 => "C1",
        >= 41 => "C2",
        >= 33 => "D",
        _ => "E",
    };
}
