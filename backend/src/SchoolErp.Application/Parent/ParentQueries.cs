using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Parent;

/// <summary>A child as shown in the parent app.</summary>
public sealed record ChildDto(
    Guid StudentId,
    string FullName,
    string AdmissionNumber,
    string? ClassName,
    string? SectionName,
    int? RollNumber,
    string? PhotoUrl);

/// <summary>
/// Resolves the guardian record for the signed-in parent (by linked user id,
/// falling back to the verified phone) and guards child access. Everything the
/// parent app reads goes through this.
/// </summary>
public sealed class ParentAccess
{
    private readonly IApplicationDbContext _db;

    public ParentAccess(IApplicationDbContext db) => _db = db;

    /// <summary>Student ids the parent may see. Empty when no guardian matches.</summary>
    public async Task<IReadOnlyList<Guid>> GetChildIdsAsync(
        string? userId, string? userPhone, CancellationToken ct)
    {
        // A failed parse leaves userGuid empty, which simply skips the id match.
        _ = Guid.TryParse(userId, out var userGuid);

        var guardianIds = await _db.Guardians
            .Where(g => (userGuid != Guid.Empty && g.UserId == userGuid) ||
                        (userPhone != null && g.Phone == userPhone))
            .Select(g => g.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (guardianIds.Count == 0)
        {
            return [];
        }

        return await _db.StudentGuardians
            .Where(sg => guardianIds.Contains(sg.GuardianId))
            .Select(sg => sg.StudentId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>Throws NotFound (never Forbidden — no information leak) unless the child is theirs.</summary>
    public async Task EnsureChildAsync(
        string? userId, string? userPhone, Guid studentId, CancellationToken ct)
    {
        var children = await GetChildIdsAsync(userId, userPhone, ct).ConfigureAwait(false);
        if (!children.Contains(studentId))
        {
            throw new NotFoundException(nameof(Student), studentId);
        }
    }
}

/// <summary>The signed-in parent's children with current placement.</summary>
public sealed record GetMyChildrenQuery(string? UserId, string? UserPhone)
    : IRequest<IReadOnlyList<ChildDto>>;

/// <summary>Composes the children list.</summary>
public sealed class GetMyChildrenQueryHandler
    : IRequestHandler<GetMyChildrenQuery, IReadOnlyList<ChildDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ParentAccess _access;

    public GetMyChildrenQueryHandler(IApplicationDbContext db, ParentAccess access)
    {
        _db = db;
        _access = access;
    }

    public async Task<IReadOnlyList<ChildDto>> Handle(
        GetMyChildrenQuery request, CancellationToken cancellationToken)
    {
        var childIds = await _access.GetChildIdsAsync(request.UserId, request.UserPhone, cancellationToken)
            .ConfigureAwait(false);
        if (childIds.Count == 0)
        {
            return [];
        }

        var currentYearId = await _db.AcademicYears
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return await _db.Students.AsNoTracking()
            .Where(s => childIds.Contains(s.Id) && s.Status == StudentStatus.Active)
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new ChildDto(
                s.Id,
                (s.FirstName + " " + s.LastName).Trim(),
                s.AdmissionNumber,
                s.Enrollments
                    .Where(e => currentYearId != null && e.AcademicYearId == currentYearId)
                    .Select(e => e.SchoolClass!.Name).FirstOrDefault(),
                s.Enrollments
                    .Where(e => currentYearId != null && e.AcademicYearId == currentYearId)
                    .Select(e => e.Section!.Name).FirstOrDefault(),
                s.Enrollments
                    .Where(e => currentYearId != null && e.AcademicYearId == currentYearId)
                    .Select(e => e.RollNumber).FirstOrDefault(),
                s.PhotoUrl))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
