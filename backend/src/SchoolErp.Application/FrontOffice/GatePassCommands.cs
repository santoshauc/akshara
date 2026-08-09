using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.FrontOffice;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.FrontOffice;

/// <summary>An early-release record.</summary>
public sealed record GatePassDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string? ClassName,
    string PassNumber,
    string Reason,
    string ReleasedTo,
    string? ReleasedToPhone,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ReturnedAt);

/// <summary>Releases a student to a named adult.</summary>
public sealed record IssueGatePassCommand(
    Guid StudentId,
    string Reason,
    string ReleasedTo,
    string? ReleasedToPhone) : IRequest<GatePassDto>;

/// <summary>Shape rules — the released-to name is the accountability record.</summary>
public sealed class IssueGatePassCommandValidator : AbstractValidator<IssueGatePassCommand>
{
    public IssueGatePassCommandValidator()
    {
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(256);
        RuleFor(c => c.ReleasedTo).NotEmpty().MaximumLength(128);
        RuleFor(c => c.ReleasedToPhone).MaximumLength(20)
            .Matches(@"^\+?[0-9 ]{6,20}$")
            .When(c => !string.IsNullOrWhiteSpace(c.ReleasedToPhone));
    }
}

/// <summary>Marks the student back on the premises.</summary>
public sealed record MarkGatePassReturnedCommand(Guid GatePassId) : IRequest<GatePassDto>;

/// <summary>Passes issued on a day (defaults to today).</summary>
public sealed record GetGatePassesQuery(DateOnly? Date) : IRequest<IReadOnlyList<GatePassDto>>;

/// <summary>Issues the pass and tells the primary guardian it happened.</summary>
public sealed class IssueGatePassCommandHandler : IRequestHandler<IssueGatePassCommand, GatePassDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLookup _tenantLookup;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public IssueGatePassCommandHandler(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        ITenantLookup tenantLookup,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _tenantLookup = tenantLookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Parents read wall-clock time, not UTC. Falls back to IST, the default
    /// for every school on the platform, if the id is missing or unknown.
    /// </summary>
    private static DateTimeOffset LocalTime(DateTimeOffset instant, string? timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(
                string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Kolkata" : timeZoneId);
            return TimeZoneInfo.ConvertTime(instant, zone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.ConvertTime(
                instant, TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "IST", "IST"));
        }
    }

    public async Task<GatePassDto> Handle(
        IssueGatePassCommand request, CancellationToken cancellationToken)
    {
        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == request.StudentId)
            .Select(s => new { s.Id, s.FirstName, s.LastName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var now = _clock.GetUtcNow();
        // Sequential per year; the unique index arbitrates concurrent desks.
        var prefix = $"GP-{now.Year}-";
        var count = await _db.GatePasses
            .CountAsync(g => g.PassNumber.StartsWith(prefix), cancellationToken)
            .ConfigureAwait(false);

        var pass = new GatePass
        {
            StudentId = student.Id,
            PassNumber = $"{prefix}{count + 1:D4}",
            Reason = request.Reason.Trim(),
            ReleasedTo = request.ReleasedTo.Trim(),
            ReleasedToPhone = string.IsNullOrWhiteSpace(request.ReleasedToPhone)
                ? null
                : request.ReleasedToPhone.Trim(),
            IssuedAt = now,
            ApprovedByUserId = Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null,
        };
        _db.GatePasses.Add(pass);

        // A child leaving early is exactly the event a parent must hear about.
        var guardianPhone = await _db.StudentGuardians
            .Where(sg => sg.StudentId == student.Id && sg.IsPrimary && sg.Guardian != null)
            .Select(sg => sg.Guardian!.Phone)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (guardianPhone is not null)
        {
            var tenant = await _tenantLookup
                .FindByIdAsync(_tenantContext.TenantId, cancellationToken)
                .ConfigureAwait(false);
            await Notifications.NotificationQueue.QueueGuardianAsync(
                _db, _tenantContext.TenantId, guardianPhone,
                "Early release",
                $"{student.FirstName} left {tenant?.Name ?? "school"} at " +
                $"{LocalTime(now, tenant?.TimeZoneId):HH:mm} with {pass.ReleasedTo}. " +
                $"Pass {pass.PassNumber}.",
                cancellationToken).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new GatePassDto(
            pass.Id, student.Id, $"{student.FirstName} {student.LastName}".Trim(), null,
            pass.PassNumber, pass.Reason, pass.ReleasedTo, pass.ReleasedToPhone,
            pass.IssuedAt, pass.ReturnedAt);
    }
}

/// <summary>Closes the pass when the student comes back.</summary>
public sealed class MarkGatePassReturnedCommandHandler
    : IRequestHandler<MarkGatePassReturnedCommand, GatePassDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public MarkGatePassReturnedCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<GatePassDto> Handle(
        MarkGatePassReturnedCommand request, CancellationToken cancellationToken)
    {
        var pass = await _db.GatePasses
            .FirstOrDefaultAsync(g => g.Id == request.GatePassId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Gate pass", request.GatePassId);

        pass.ReturnedAt ??= _clock.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == pass.StudentId)
            .Select(s => (s.FirstName + " " + s.LastName).Trim())
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        return new GatePassDto(
            pass.Id, pass.StudentId, student, null, pass.PassNumber, pass.Reason,
            pass.ReleasedTo, pass.ReleasedToPhone, pass.IssuedAt, pass.ReturnedAt);
    }
}

/// <summary>Reads a day's passes with the student's current class.</summary>
public sealed class GetGatePassesQueryHandler
    : IRequestHandler<GetGatePassesQuery, IReadOnlyList<GatePassDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetGatePassesQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<GatePassDto>> Handle(
        GetGatePassesQuery request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddDays(1);

        var rows = await _db.GatePasses.AsNoTracking()
            .Where(g => g.IssuedAt >= from && g.IssuedAt < to)
            .OrderByDescending(g => g.IssuedAt)
            .Select(g => new
            {
                Pass = g,
                StudentName = (g.Student!.FirstName + " " + g.Student.LastName).Trim(),
                ClassName = g.Student.Enrollments
                    .Where(e => e.AcademicYear!.IsCurrent)
                    .Select(e => e.SchoolClass!.Name + " " + (e.Section == null ? "" : e.Section.Name))
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new GatePassDto(
                r.Pass.Id, r.Pass.StudentId, r.StudentName, r.ClassName?.Trim(),
                r.Pass.PassNumber, r.Pass.Reason, r.Pass.ReleasedTo, r.Pass.ReleasedToPhone,
                r.Pass.IssuedAt, r.Pass.ReturnedAt))
            .ToList();
    }
}
