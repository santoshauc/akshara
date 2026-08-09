using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Attendance.Queries;

/// <summary>
/// The marking grid: every active student of a section with the day's status.
/// A null <paramref name="Period"/> shows the daily roll call (the default);
/// a period number shows that timetable slot's marks.
/// </summary>
public sealed record GetSectionAttendanceQuery(
    Guid SectionId, DateOnly Date, int? Period = null)
    : IRequest<SectionAttendanceDto>;

/// <summary>Composes roster + existing records for the date.</summary>
public sealed class GetSectionAttendanceQueryHandler
    : IRequestHandler<GetSectionAttendanceQuery, SectionAttendanceDto>
{
    private readonly IApplicationDbContext _db;

    public GetSectionAttendanceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<SectionAttendanceDto> Handle(
        GetSectionAttendanceQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Sections.AnyAsync(s => s.Id == request.SectionId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Section", request.SectionId);
        }

        var roster = await _db.Enrollments.AsNoTracking()
            .Where(e => e.SectionId == request.SectionId && e.Status == EnrollmentStatus.Active)
            .Select(e => new
            {
                e.Id,
                e.StudentId,
                e.RollNumber,
                Student = _db.Students.Where(s => s.Id == e.StudentId)
                    .Select(s => new { s.FirstName, s.LastName, s.AdmissionNumber })
                    .First(),
                Record = _db.AttendanceRecords
                    .Where(a => a.EnrollmentId == e.Id && a.Date == request.Date &&
                                a.Period == request.Period)
                    .Select(a => new { a.Status, a.Remarks })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entries = roster
            .Select(r => new RosterEntryDto
            {
                EnrollmentId = r.Id,
                StudentId = r.StudentId,
                StudentName = $"{r.Student.FirstName} {r.Student.LastName}".Trim(),
                AdmissionNumber = r.Student.AdmissionNumber,
                RollNumber = r.RollNumber,
                Status = r.Record?.Status,
                Remarks = r.Record?.Remarks,
            })
            .OrderBy(r => r.RollNumber ?? int.MaxValue).ThenBy(r => r.StudentName)
            .ToList();

        return new SectionAttendanceDto
        {
            SectionId = request.SectionId,
            Date = request.Date,
            IsMarked = entries.Any(e => e.Status is not null),
            Roster = entries,
        };
    }
}

/// <summary>A student's month calendar with counters.</summary>
public sealed record GetStudentMonthAttendanceQuery(Guid StudentId, int Year, int Month)
    : IRequest<StudentMonthAttendanceDto>;

/// <summary>Month bounds.</summary>
public sealed class GetStudentMonthAttendanceQueryValidator
    : AbstractValidator<GetStudentMonthAttendanceQuery>
{
    public GetStudentMonthAttendanceQueryValidator()
    {
        RuleFor(q => q.Year).InclusiveBetween(2000, 2100);
        RuleFor(q => q.Month).InclusiveBetween(1, 12);
    }
}

/// <summary>Aggregates one month of records for the calendar UI.</summary>
public sealed class GetStudentMonthAttendanceQueryHandler
    : IRequestHandler<GetStudentMonthAttendanceQuery, StudentMonthAttendanceDto>
{
    private readonly IApplicationDbContext _db;

    public GetStudentMonthAttendanceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<StudentMonthAttendanceDto> Handle(
        GetStudentMonthAttendanceQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(Student), request.StudentId);
        }

        var monthStart = new DateOnly(request.Year, request.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // The calendar shows the daily roll call only; per-period rows are a
        // staff drill-down and would double-count here.
        var days = await _db.AttendanceRecords.AsNoTracking()
            .Where(a => a.StudentId == request.StudentId &&
                        a.Date >= monthStart && a.Date < monthEnd &&
                        a.Period == null)
            .OrderBy(a => a.Date)
            .Select(a => new AttendanceDayDto(a.Date, a.Status, a.Remarks))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int Count(AttendanceStatus status) => days.Count(d => d.Status == status);

        var present = Count(AttendanceStatus.Present);
        var late = Count(AttendanceStatus.Late);
        var halfDay = Count(AttendanceStatus.HalfDay);
        var leave = Count(AttendanceStatus.Leave);
        var attended = present + late + halfDay;
        // Approved leave is excused: it neither counts as attended nor drags
        // the percentage down — the denominator is the accountable days.
        var accountable = days.Count - leave;

        return new StudentMonthAttendanceDto
        {
            StudentId = request.StudentId,
            Year = request.Year,
            Month = request.Month,
            Days = days,
            PresentCount = present,
            AbsentCount = Count(AttendanceStatus.Absent),
            LateCount = late,
            HalfDayCount = halfDay,
            LeaveCount = leave,
            MarkedDays = days.Count,
            AttendancePercent = accountable == 0
                ? 0
                : Math.Round(attended * 100.0 / accountable, 1),
        };
    }
}
