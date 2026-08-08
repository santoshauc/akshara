using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Attendance.Commands;

/// <summary>One student's status inside a marking submission.</summary>
public sealed record AttendanceEntry(Guid EnrollmentId, AttendanceStatus Status, string? Remarks);

/// <summary>
/// Marks (or re-marks) attendance for a section on a date. Upserts one record
/// per enrollment. Students newly marked Absent trigger an SMS to their
/// primary guardian via the transactional outbox — written in the same
/// transaction, delivered asynchronously by the dispatcher.
/// </summary>
public sealed record MarkAttendanceCommand(
    Guid SectionId,
    DateOnly Date,
    IReadOnlyList<AttendanceEntry> Entries) : IRequest<int>;

/// <summary>Marking shape rules.</summary>
public sealed class MarkAttendanceCommandValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceCommandValidator(TimeProvider clock)
    {
        RuleFor(c => c.Entries).NotEmpty()
            .Must(e => e.Select(x => x.EnrollmentId).Distinct().Count() == e.Count)
            .WithMessage("Each enrollment may appear only once.");

        RuleForEach(c => c.Entries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Status).IsInEnum();
            entry.RuleFor(e => e.Remarks).MaximumLength(256);
        });

        RuleFor(c => c.Date)
            .Must(d => d <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddDays(1)))
            .WithMessage("Attendance cannot be marked for future dates.");
    }
}

/// <summary>Upserts the day's records and queues absence notifications.</summary>
public sealed class MarkAttendanceCommandHandler : IRequestHandler<MarkAttendanceCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLookup _tenantLookup;

    public MarkAttendanceCommandHandler(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        ITenantLookup tenantLookup)
    {
        _db = db;
        _tenantContext = tenantContext;
        _tenantLookup = tenantLookup;
    }

    public async Task<int> Handle(MarkAttendanceCommand request, CancellationToken cancellationToken)
    {
        // Active placements of this section — tenant filters make this safe.
        var enrollments = await _db.Enrollments
            .Where(e => e.SectionId == request.SectionId && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.Id, e.StudentId })
            .ToDictionaryAsync(e => e.Id, e => e.StudentId, cancellationToken)
            .ConfigureAwait(false);

        if (enrollments.Count == 0)
        {
            throw new NotFoundException("Section (with active enrollments)", request.SectionId);
        }

        var unknown = request.Entries.FirstOrDefault(e => !enrollments.ContainsKey(e.EnrollmentId));
        if (unknown is not null)
        {
            throw new NotFoundException("Enrollment (in this section)", unknown.EnrollmentId);
        }

        var existing = await _db.AttendanceRecords
            .Where(a => a.SectionId == request.SectionId && a.Date == request.Date)
            .ToDictionaryAsync(a => a.EnrollmentId, cancellationToken)
            .ConfigureAwait(false);

        var newlyAbsentStudentIds = new List<Guid>();

        foreach (var entry in request.Entries)
        {
            if (existing.TryGetValue(entry.EnrollmentId, out var record))
            {
                var wasAbsent = record.Status == AttendanceStatus.Absent;
                record.Status = entry.Status;
                record.Remarks = entry.Remarks;
                if (!wasAbsent && entry.Status == AttendanceStatus.Absent)
                {
                    newlyAbsentStudentIds.Add(record.StudentId);
                }
            }
            else
            {
                var studentId = enrollments[entry.EnrollmentId];
                _db.AttendanceRecords.Add(new AttendanceRecord
                {
                    EnrollmentId = entry.EnrollmentId,
                    StudentId = studentId,
                    SectionId = request.SectionId,
                    Date = request.Date,
                    Status = entry.Status,
                    Remarks = entry.Remarks,
                });
                if (entry.Status == AttendanceStatus.Absent)
                {
                    newlyAbsentStudentIds.Add(studentId);
                }
            }
        }

        await QueueAbsenceNotificationsAsync(newlyAbsentStudentIds, request.Date, cancellationToken)
            .ConfigureAwait(false);

        return await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Outbox rows for primary guardians of newly-absent students.</summary>
    private async Task QueueAbsenceNotificationsAsync(
        IReadOnlyList<Guid> studentIds, DateOnly date, CancellationToken ct)
    {
        if (studentIds.Count == 0)
        {
            return;
        }

        var tenant = await _tenantLookup.FindByIdAsync(_tenantContext.TenantId, ct).ConfigureAwait(false);
        var schoolName = tenant?.Name ?? "your school";

        var contacts = await _db.StudentGuardians
            .Where(sg => studentIds.Contains(sg.StudentId) && sg.IsPrimary && sg.Guardian != null)
            .Select(sg => new
            {
                sg.StudentId,
                sg.Guardian!.Phone,
                StudentName = _db.Students
                    .Where(s => s.Id == sg.StudentId)
                    .Select(s => s.FirstName)
                    .First(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var contact in contacts)
        {
            await Notifications.NotificationQueue.QueueGuardianAsync(
                _db, _tenantContext.TenantId, contact.Phone,
                "Absence noted",
                $"{contact.StudentName} was marked absent on {date:dd MMM yyyy} at {schoolName}. " +
                "Please contact the school if this is unexpected.",
                ct).ConfigureAwait(false);
        }
    }
}
