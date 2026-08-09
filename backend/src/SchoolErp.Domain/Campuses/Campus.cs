using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Campuses;

/// <summary>
/// A physical location an institution operates from. Multi-campus is ordinary
/// in Indian education — a trust runs a junior wing and a senior wing on
/// different roads, a college has a city campus and an outer-ring campus —
/// and each has its own address and phone that certificates and parents need.
/// <para>
/// Tenant-scoped (RLS): a campus belongs to one school, not to the platform.
/// </para>
/// </summary>
public class Campus : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Short label used in lists and on rolls ("MAIN", "NORTH").</summary>
    public string Code { get; set; } = string.Empty;

    public string? AddressLine1 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? ContactPhone { get; set; }

    /// <summary>
    /// The head campus. Exactly one per institution: it is what letterheads
    /// and any single-address view fall back to.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>Closed campuses are kept for history rather than deleted.</summary>
    public bool IsActive { get; set; } = true;
}
