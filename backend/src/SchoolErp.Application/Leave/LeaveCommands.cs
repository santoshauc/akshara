using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Leave;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Leave;

/// <summary>
/// Files a leave request. Student requests carry the child's id (the caller
/// must already be authorized for that student — the parent endpoint guards
/// this); staff requests carry neither and are linked to the caller.
/// </summary>
public sealed record SubmitLeaveRequestCommand(
    Guid? StudentId,
    DateOnly FromDate,
    DateOnly ToDate,
    string Reason) : IRequest<Guid>;

/// <summary>Range and reason shape rules.</summary>
public sealed class SubmitLeaveRequestCommandValidator
    : AbstractValidator<SubmitLeaveRequestCommand>
{
    public SubmitLeaveRequestCommandValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(512);
        RuleFor(c => c.ToDate)
            .GreaterThanOrEqualTo(c => c.FromDate)
            .WithMessage("The end date must not be before the start date.");
        RuleFor(c => c)
            .Must(c => c.ToDate.DayNumber - c.FromDate.DayNumber < 31)
            .WithMessage("A leave request may cover at most 31 days.");
    }
}

/// <summary>Creates the pending request for the signed-in user.</summary>
public sealed class SubmitLeaveRequestCommandHandler
    : IRequestHandler<SubmitLeaveRequestCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SubmitLeaveRequestCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        SubmitLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            throw new NotFoundException("User", _currentUser.UserId ?? "(anonymous)");
        }

        Guid? teacherId = null;
        if (request.StudentId is { } studentId)
        {
            if (!await _db.Students.AnyAsync(s => s.Id == studentId, cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new NotFoundException(nameof(Student), studentId);
            }
        }
        else
        {
            // Staff request: link the teacher record when the account has one.
            teacherId = await _db.Teachers
                .Where(t => t.UserId == userId)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var overlapping = await _db.LeaveRequests.AnyAsync(l =>
                l.Status != LeaveRequestStatus.Rejected &&
                l.FromDate <= request.ToDate && request.FromDate <= l.ToDate &&
                (request.StudentId != null
                    ? l.StudentId == request.StudentId
                    : l.RequestedByUserId == userId && l.StudentId == null),
            cancellationToken).ConfigureAwait(false);
        if (overlapping)
        {
            throw new ConflictException(
                "A leave request already covers part of that date range.");
        }

        var leave = new LeaveRequest
        {
            Kind = request.StudentId is null
                ? LeaveApplicantKind.Staff
                : LeaveApplicantKind.Student,
            StudentId = request.StudentId,
            TeacherId = teacherId,
            RequestedByUserId = userId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Reason = request.Reason.Trim(),
        };
        _db.LeaveRequests.Add(leave);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return leave.Id;
    }
}

/// <summary>
/// Approves or rejects a pending request. Approving a student request marks
/// every day in the range as Leave in attendance (upserting over any earlier
/// mark) for the student's current-year enrollment.
/// </summary>
public sealed record DecideLeaveRequestCommand(
    Guid LeaveRequestId,
    bool Approve,
    string? DecisionNote) : IRequest;

/// <summary>Note length rule.</summary>
public sealed class DecideLeaveRequestCommandValidator
    : AbstractValidator<DecideLeaveRequestCommand>
{
    public DecideLeaveRequestCommandValidator() =>
        RuleFor(c => c.DecisionNote).MaximumLength(512);
}

/// <summary>Applies the decision and the attendance side effect.</summary>
public sealed class DecideLeaveRequestCommandHandler : IRequestHandler<DecideLeaveRequestCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public DecideLeaveRequestCommandHandler(
        IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task Handle(DecideLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var leave = await _db.LeaveRequests
            .FirstOrDefaultAsync(l => l.Id == request.LeaveRequestId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(LeaveRequest), request.LeaveRequestId);

        if (leave.Status != LeaveRequestStatus.Pending)
        {
            throw new ConflictException("This request has already been decided.");
        }

        leave.Status = request.Approve
            ? LeaveRequestStatus.Approved
            : LeaveRequestStatus.Rejected;
        leave.DecidedByUserId = Guid.TryParse(_currentUser.UserId, out var decider)
            ? decider
            : null;
        leave.DecidedAt = _clock.GetUtcNow();
        leave.DecisionNote = string.IsNullOrWhiteSpace(request.DecisionNote)
            ? null
            : request.DecisionNote.Trim();

        if (request.Approve && leave.StudentId is { } studentId)
        {
            await MarkRangeAsLeaveAsync(studentId, leave, cancellationToken)
                .ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task MarkRangeAsLeaveAsync(
        Guid studentId, LeaveRequest leave, CancellationToken ct)
    {
        var currentYearId = await _db.AcademicYears
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var enrollment = await _db.Enrollments
            .Where(e => e.StudentId == studentId &&
                        e.Status == EnrollmentStatus.Active &&
                        (currentYearId == null || e.AcademicYearId == currentYearId))
            .Select(e => new { e.Id, e.SectionId })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (enrollment is null)
        {
            return; // Not enrolled this year — nothing to mark; the approval still stands.
        }

        var existing = await _db.AttendanceRecords
            .Where(a => a.EnrollmentId == enrollment.Id && a.Period == null &&
                        a.Date >= leave.FromDate && a.Date <= leave.ToDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var byDate = existing.ToDictionary(a => a.Date);

        for (var date = leave.FromDate; date <= leave.ToDate; date = date.AddDays(1))
        {
            if (byDate.TryGetValue(date, out var record))
            {
                record.Status = AttendanceStatus.Leave;
                record.Remarks = "Approved leave";
            }
            else
            {
                _db.AttendanceRecords.Add(new AttendanceRecord
                {
                    EnrollmentId = enrollment.Id,
                    StudentId = studentId,
                    SectionId = enrollment.SectionId,
                    Date = date,
                    Status = AttendanceStatus.Leave,
                    Remarks = "Approved leave",
                });
            }
        }
    }
}
