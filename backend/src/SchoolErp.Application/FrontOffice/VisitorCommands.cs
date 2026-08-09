using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.FrontOffice;

namespace SchoolErp.Application.FrontOffice;

/// <summary>A row of the gate register.</summary>
public sealed record VisitorEntryDto(
    Guid Id,
    string VisitorName,
    string? Phone,
    VisitorPurpose Purpose,
    string? WhomToMeet,
    Guid? StudentId,
    string? StudentName,
    string PassNumber,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    string? Remarks);

/// <summary>Signs a visitor in and issues a badge number.</summary>
public sealed record CheckInVisitorCommand(
    string VisitorName,
    string? Phone,
    VisitorPurpose Purpose,
    string? WhomToMeet,
    Guid? StudentId,
    string? Remarks) : IRequest<VisitorEntryDto>;

/// <summary>Shape rules for the desk form.</summary>
public sealed class CheckInVisitorCommandValidator : AbstractValidator<CheckInVisitorCommand>
{
    public CheckInVisitorCommandValidator()
    {
        RuleFor(c => c.VisitorName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Phone).MaximumLength(20)
            .Matches(@"^\+?[0-9 ]{6,20}$")
            .When(c => !string.IsNullOrWhiteSpace(c.Phone));
        RuleFor(c => c.Purpose).IsInEnum();
        RuleFor(c => c.WhomToMeet).MaximumLength(128);
        RuleFor(c => c.Remarks).MaximumLength(512);
    }
}

/// <summary>Signs a visitor out; idempotent once closed.</summary>
public sealed record CheckOutVisitorCommand(Guid VisitorEntryId) : IRequest<VisitorEntryDto>;

/// <summary>The register: open visits first, newest first within each group.</summary>
public sealed record GetVisitorsQuery(DateOnly? Date, bool OpenOnly = false)
    : IRequest<IReadOnlyList<VisitorEntryDto>>;

/// <summary>Issues the badge number and records the arrival.</summary>
public sealed class CheckInVisitorCommandHandler
    : IRequestHandler<CheckInVisitorCommand, VisitorEntryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CheckInVisitorCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<VisitorEntryDto> Handle(
        CheckInVisitorCommand request, CancellationToken cancellationToken)
    {
        if (request.StudentId is { } studentId &&
            !await _db.Students.AnyAsync(s => s.Id == studentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Student", studentId);
        }

        var now = _clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var dayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Badges restart every morning — "V-12" means the twelfth visitor today.
        var todayCount = await _db.VisitorEntries
            .CountAsync(v => v.CheckedInAt >= dayStart, cancellationToken)
            .ConfigureAwait(false);

        var entry = new VisitorEntry
        {
            VisitorName = request.VisitorName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Purpose = request.Purpose,
            WhomToMeet = string.IsNullOrWhiteSpace(request.WhomToMeet)
                ? null
                : request.WhomToMeet.Trim(),
            StudentId = request.StudentId,
            PassNumber = $"V-{today:yyyyMMdd}-{todayCount + 1:D3}",
            CheckedInAt = now,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
        };
        _db.VisitorEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var studentName = entry.StudentId is null
            ? null
            : await _db.Students.AsNoTracking()
                .Where(s => s.Id == entry.StudentId)
                .Select(s => (s.FirstName + " " + s.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

        return ToDto(entry, studentName);
    }

    internal static VisitorEntryDto ToDto(VisitorEntry entry, string? studentName) =>
        new(entry.Id, entry.VisitorName, entry.Phone, entry.Purpose, entry.WhomToMeet,
            entry.StudentId, studentName, entry.PassNumber, entry.CheckedInAt,
            entry.CheckedOutAt, entry.Remarks);
}

/// <summary>Closes the visit.</summary>
public sealed class CheckOutVisitorCommandHandler
    : IRequestHandler<CheckOutVisitorCommand, VisitorEntryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CheckOutVisitorCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<VisitorEntryDto> Handle(
        CheckOutVisitorCommand request, CancellationToken cancellationToken)
    {
        var entry = await _db.VisitorEntries
            .FirstOrDefaultAsync(v => v.Id == request.VisitorEntryId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Visitor entry", request.VisitorEntryId);

        // Signing out twice is a double-click, not an error — keep the first time.
        entry.CheckedOutAt ??= _clock.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var studentName = entry.StudentId is null
            ? null
            : await _db.Students.AsNoTracking()
                .Where(s => s.Id == entry.StudentId)
                .Select(s => (s.FirstName + " " + s.LastName).Trim())
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

        return CheckInVisitorCommandHandler.ToDto(entry, studentName);
    }
}

/// <summary>Reads the register for a day, or everyone still inside.</summary>
public sealed class GetVisitorsQueryHandler
    : IRequestHandler<GetVisitorsQuery, IReadOnlyList<VisitorEntryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetVisitorsQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<VisitorEntryDto>> Handle(
        GetVisitorsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.VisitorEntries.AsNoTracking();

        if (request.OpenOnly)
        {
            query = query.Where(v => v.CheckedOutAt == null);
        }
        else
        {
            var date = request.Date ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var to = from.AddDays(1);
            query = query.Where(v => v.CheckedInAt >= from && v.CheckedInAt < to);
        }

        var rows = await query
            .OrderByDescending(v => v.CheckedInAt)
            .Select(v => new
            {
                Entry = v,
                StudentName = v.Student == null
                    ? null
                    : (v.Student.FirstName + " " + v.Student.LastName).Trim(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => CheckInVisitorCommandHandler.ToDto(r.Entry, r.StudentName))
            .ToList();
    }
}
