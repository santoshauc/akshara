using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;
using SchoolErp.Shared.Models;

namespace SchoolErp.Application.Students.Queries;

/// <summary>
/// Paged student listing with search and placement filters. Placement columns
/// come from the enrollment of <paramref name="AcademicYearId"/> (defaults to
/// the current academic year).
/// </summary>
public sealed record GetStudentsQuery(
    string? Search = null,
    Guid? AcademicYearId = null,
    Guid? SchoolClassId = null,
    Guid? SectionId = null,
    StudentStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<StudentListItemDto>>;

/// <summary>Pagination bounds.</summary>
public sealed class GetStudentsQueryValidator : AbstractValidator<GetStudentsQuery>
{
    public GetStudentsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>Composes the list in SQL: students left-joined to the year's enrollment.</summary>
public sealed class GetStudentsQueryHandler
    : IRequestHandler<GetStudentsQuery, PagedResult<StudentListItemDto>>
{
    private readonly IApplicationDbContext _db;

    public GetStudentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<StudentListItemDto>> Handle(
        GetStudentsQuery request, CancellationToken cancellationToken)
    {
        var yearId = request.AcademicYearId
            ?? await _db.AcademicYears.Where(y => y.IsCurrent)
                .Select(y => (Guid?)y.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

        var students = _db.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";
            students = students.Where(s =>
                EF.Functions.ILike(s.FirstName, term) ||
                EF.Functions.ILike(s.LastName, term) ||
                EF.Functions.ILike(s.AdmissionNumber, term));
        }

        if (request.Status is { } status)
        {
            students = students.Where(s => s.Status == status);
        }

        // Left join to the selected year's enrollment for placement columns.
        var query = students.Select(s => new
        {
            Student = s,
            Enrollment = s.Enrollments
                .Where(e => yearId != null && e.AcademicYearId == yearId)
                .Select(e => new { e.SchoolClassId, e.SectionId, e.RollNumber, ClassName = e.SchoolClass!.Name, SectionName = e.Section!.Name })
                .FirstOrDefault(),
        });

        if (request.SchoolClassId is { } classId)
        {
            query = query.Where(x => x.Enrollment != null && x.Enrollment.SchoolClassId == classId);
        }

        if (request.SectionId is { } sectionId)
        {
            query = query.Where(x => x.Enrollment != null && x.Enrollment.SectionId == sectionId);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(x => x.Student.FirstName).ThenBy(x => x.Student.LastName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new StudentListItemDto
            {
                Id = x.Student.Id,
                AdmissionNumber = x.Student.AdmissionNumber,
                FirstName = x.Student.FirstName,
                LastName = x.Student.LastName,
                Gender = x.Student.Gender,
                Status = x.Student.Status,
                ClassName = x.Enrollment != null ? x.Enrollment.ClassName : null,
                SectionName = x.Enrollment != null ? x.Enrollment.SectionName : null,
                RollNumber = x.Enrollment != null ? x.Enrollment.RollNumber : null,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<StudentListItemDto>(items, total, request.Page, request.PageSize);
    }
}

/// <summary>Full student detail with guardians and the latest active enrollment.</summary>
public sealed record GetStudentByIdQuery(Guid Id) : IRequest<StudentDetailDto>;

/// <summary>Returns the student or 404s.</summary>
public sealed class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, StudentDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetStudentByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<StudentDetailDto> Handle(
        GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        var student = await _db.Students.AsNoTracking()
            .Include(s => s.Guardians).ThenInclude(sg => sg.Guardian)
            .Where(s => s.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.Id);

        var enrollment = await _db.Enrollments.AsNoTracking()
            .Include(e => e.AcademicYear)
            .Include(e => e.SchoolClass)
            .Include(e => e.Section)
            .Where(e => e.StudentId == student.Id && e.Status == EnrollmentStatus.Active)
            .OrderByDescending(e => e.AcademicYear!.StartDate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new StudentDetailDto
        {
            Id = student.Id,
            AdmissionNumber = student.AdmissionNumber,
            FirstName = student.FirstName,
            LastName = student.LastName,
            DateOfBirth = student.DateOfBirth,
            Gender = student.Gender,
            BloodGroup = student.BloodGroup,
            Email = student.Email,
            Phone = student.Phone,
            AddressLine1 = student.AddressLine1,
            City = student.City,
            State = student.State,
            PostalCode = student.PostalCode,
            MedicalNotes = student.MedicalNotes,
            AdmissionDate = student.AdmissionDate,
            Status = student.Status,
            Guardians = student.Guardians
                .Where(sg => sg.Guardian is not null)
                .Select(sg => new GuardianDto
                {
                    Id = sg.Guardian!.Id,
                    FirstName = sg.Guardian.FirstName,
                    LastName = sg.Guardian.LastName,
                    Relation = sg.Guardian.Relation,
                    Phone = sg.Guardian.Phone,
                    Email = sg.Guardian.Email,
                    Occupation = sg.Guardian.Occupation,
                    IsPrimary = sg.IsPrimary,
                })
                .OrderByDescending(g => g.IsPrimary)
                .ToList(),
            CurrentEnrollment = enrollment?.ToDto(),
        };
    }
}
