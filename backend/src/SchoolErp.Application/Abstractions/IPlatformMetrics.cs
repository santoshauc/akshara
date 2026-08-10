namespace SchoolErp.Application.Abstractions;

/// <summary>What one school actually contains, counted past its RLS policy.</summary>
public sealed record TenantCounts(
    Guid TenantId,
    int ActiveStudents,
    int ActiveTeachers,
    int Guardians,
    int OpenCampuses);

/// <summary>
/// The two things the platform dashboard needs that ordinary application
/// queries cannot reach: counts inside RLS'd tenant tables, and the identity
/// store. Both are infrastructure concerns — the first needs a
/// SECURITY DEFINER function, the second needs the Identity context — so they
/// sit behind this port rather than leaking into a handler.
/// </summary>
public interface IPlatformMetrics
{
    /// <summary>
    /// Per-school counts for every live school, in one round trip. Returns
    /// counts only: no name, id or row of tenant data crosses this boundary
    /// beyond the tenant id needed to attribute them.
    /// </summary>
    Task<IReadOnlyList<TenantCounts>> GetTenantCountsAsync(CancellationToken cancellationToken);

    /// <summary>Portal/staff accounts that belong to a school, platform operators excluded.</summary>
    Task<int> CountSchoolUsersAsync(CancellationToken cancellationToken);
}
