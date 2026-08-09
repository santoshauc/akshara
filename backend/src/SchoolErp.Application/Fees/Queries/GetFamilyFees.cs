using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Parent;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Fees.Queries;

/// <summary>One child's line in the family fee view.</summary>
public sealed record FamilyChildFeeDto(
    Guid StudentId,
    string StudentName,
    string? ClassName,
    decimal TotalDue,
    decimal TotalConcession,
    decimal TotalPaid,
    decimal Balance);

/// <summary>The whole family's fee position, one row per child.</summary>
public sealed record FamilyFeeSummaryDto(
    IReadOnlyList<FamilyChildFeeDto> Children,
    decimal FamilyBalance);

/// <summary>
/// The signed-in parent's family fee view: every linked child with their
/// current-year balance and the combined total. Each child appears exactly
/// once no matter how many guardians link to them.
/// </summary>
public sealed record GetParentFamilyFeesQuery(string? UserId, string? UserPhone)
    : IRequest<FamilyFeeSummaryDto>;

/// <summary>Resolves children via the family guard, then sums per child.</summary>
public sealed class GetParentFamilyFeesQueryHandler
    : IRequestHandler<GetParentFamilyFeesQuery, FamilyFeeSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ParentAccess _access;
    private readonly ISender _sender;

    public GetParentFamilyFeesQueryHandler(
        IApplicationDbContext db, ParentAccess access, ISender sender)
    {
        _db = db;
        _access = access;
        _sender = sender;
    }

    public async Task<FamilyFeeSummaryDto> Handle(
        GetParentFamilyFeesQuery request, CancellationToken ct)
    {
        var childIds = (await _access
                .GetChildIdsAsync(request.UserId, request.UserPhone, ct)
                .ConfigureAwait(false))
            .Distinct()
            .ToList();
        return await FamilyFees.ComposeAsync(_db, _sender, childIds, ct)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// The staff-side family ledger, reached from any sibling: every student
/// sharing at least one guardian with the given student (self included).
/// </summary>
public sealed record GetStudentFamilyFeesQuery(Guid StudentId)
    : IRequest<FamilyFeeSummaryDto>;

/// <summary>Finds siblings through shared guardians, then sums per child.</summary>
public sealed class GetStudentFamilyFeesQueryHandler
    : IRequestHandler<GetStudentFamilyFeesQuery, FamilyFeeSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;

    public GetStudentFamilyFeesQueryHandler(IApplicationDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<FamilyFeeSummaryDto> Handle(
        GetStudentFamilyFeesQuery request, CancellationToken ct)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, ct)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(Student), request.StudentId);
        }

        var guardianIds = await _db.StudentGuardians.AsNoTracking()
            .Where(g => g.StudentId == request.StudentId)
            .Select(g => g.GuardianId)
            .ToListAsync(ct).ConfigureAwait(false);

        var familyIds = await _db.StudentGuardians.AsNoTracking()
            .Where(g => guardianIds.Contains(g.GuardianId))
            .Select(g => g.StudentId)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        return await FamilyFees.ComposeAsync(_db, _sender, familyIds, ct)
            .ConfigureAwait(false);
    }
}

/// <summary>Shared composition: per-child current-year summary + family total.</summary>
internal static class FamilyFees
{
    public static async Task<FamilyFeeSummaryDto> ComposeAsync(
        IApplicationDbContext db, ISender sender, IReadOnlyList<Guid> childIds,
        CancellationToken ct)
    {
        var currentYearId = await db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (currentYearId is not { } yearId || childIds.Count == 0)
        {
            return new FamilyFeeSummaryDto([], 0);
        }

        var children = await db.Students.AsNoTracking()
            .Where(s => childIds.Contains(s.Id) && s.Status == StudentStatus.Active)
            .OrderBy(s => s.FirstName)
            .Select(s => new
            {
                s.Id,
                Name = (s.FirstName + " " + s.LastName).Trim(),
                ClassName = db.Enrollments
                    .Where(e => e.StudentId == s.Id && e.AcademicYearId == yearId &&
                                e.Status == EnrollmentStatus.Active)
                    .Select(e => e.SchoolClass!.Name + " " +
                        (e.Section != null ? e.Section.Name : ""))
                    .FirstOrDefault(),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var lines = new List<FamilyChildFeeDto>();
        foreach (var child in children)
        {
            if (child.ClassName is null)
            {
                continue; // not enrolled this year — nothing owed under the plan
            }

            var summary = await sender
                .Send(new GetStudentFeeSummaryQuery(child.Id, yearId), ct)
                .ConfigureAwait(false);
            lines.Add(new FamilyChildFeeDto(
                child.Id,
                child.Name,
                child.ClassName.Trim(),
                summary.TotalDue,
                summary.TotalConcession,
                summary.TotalPaid,
                summary.Balance));
        }

        return new FamilyFeeSummaryDto(lines, lines.Sum(l => l.Balance));
    }
}
