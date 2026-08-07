using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Academics;

/// <summary>A taught subject (e.g. Mathematics, తెలుగు). Tenant-scoped.</summary>
public class Subject : TenantEntity
{
    /// <summary>Display name, unique within the tenant.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code used on report cards (e.g. "MATH").</summary>
    public string Code { get; set; } = string.Empty;
}
