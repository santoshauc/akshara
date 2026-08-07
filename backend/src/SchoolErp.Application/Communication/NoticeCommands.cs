using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Communication;

namespace SchoolErp.Application.Communication;

/// <summary>Notice projection.</summary>
public sealed record NoticeDto(
    Guid Id,
    string Title,
    string Body,
    Guid? SchoolClassId,
    string? ClassName,
    DateOnly? ExpiresOn,
    bool IsPinned,
    DateTimeOffset PublishedAt);

/// <summary>Publishes a notice (school-wide or class-scoped).</summary>
public sealed record CreateNoticeCommand(
    string Title,
    string Body,
    Guid? SchoolClassId,
    DateOnly? ExpiresOn,
    bool IsPinned) : IRequest<Guid>;

/// <summary>Notice shape rules.</summary>
public sealed class CreateNoticeCommandValidator : AbstractValidator<CreateNoticeCommand>
{
    public CreateNoticeCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(4000);
    }
}

/// <summary>Creates the notice after a class-existence check.</summary>
public sealed class CreateNoticeCommandHandler : IRequestHandler<CreateNoticeCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateNoticeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateNoticeCommand request, CancellationToken cancellationToken)
    {
        if (request.SchoolClassId is { } classId &&
            !await _db.SchoolClasses.AnyAsync(c => c.Id == classId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("SchoolClass", classId);
        }

        var notice = new Notice
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            SchoolClassId = request.SchoolClassId,
            ExpiresOn = request.ExpiresOn,
            IsPinned = request.IsPinned,
        };
        _db.Notices.Add(notice);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return notice.Id;
    }
}

/// <summary>Latest notices for staff (all scopes).</summary>
public sealed record GetNoticesQuery(int Limit = 50) : IRequest<IReadOnlyList<NoticeDto>>;

/// <summary>Pinned first, newest first.</summary>
public sealed class GetNoticesQueryHandler : IRequestHandler<GetNoticesQuery, IReadOnlyList<NoticeDto>>
{
    private readonly IApplicationDbContext _db;

    public GetNoticesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NoticeDto>> Handle(
        GetNoticesQuery request, CancellationToken cancellationToken) =>
        await _db.Notices.AsNoTracking()
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .Select(n => new NoticeDto(
                n.Id, n.Title, n.Body, n.SchoolClassId,
                n.SchoolClassId != null
                    ? _db.SchoolClasses.Where(c => c.Id == n.SchoolClassId)
                        .Select(c => c.Name).FirstOrDefault()
                    : null,
                n.ExpiresOn, n.IsPinned, n.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>
/// Notices visible to one student: school-wide plus their current class,
/// excluding expired ones.
/// </summary>
public sealed record GetStudentNoticesQuery(Guid StudentId, DateOnly Today)
    : IRequest<IReadOnlyList<NoticeDto>>;

/// <summary>Filters by the student's current-year class.</summary>
public sealed class GetStudentNoticesQueryHandler
    : IRequestHandler<GetStudentNoticesQuery, IReadOnlyList<NoticeDto>>
{
    private readonly IApplicationDbContext _db;

    public GetStudentNoticesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NoticeDto>> Handle(
        GetStudentNoticesQuery request, CancellationToken cancellationToken)
    {
        var classId = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == request.StudentId && e.AcademicYear!.IsCurrent)
            .Select(e => (Guid?)e.SchoolClassId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return await _db.Notices.AsNoTracking()
            .Where(n => (n.SchoolClassId == null || n.SchoolClassId == classId) &&
                        (n.ExpiresOn == null || n.ExpiresOn >= request.Today))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NoticeDto(
                n.Id, n.Title, n.Body, n.SchoolClassId, null,
                n.ExpiresOn, n.IsPinned, n.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
