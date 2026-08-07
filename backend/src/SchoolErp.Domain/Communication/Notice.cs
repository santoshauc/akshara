using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Communication;

/// <summary>
/// A school notice/circular. Scoped to the whole school when
/// <see cref="SchoolClassId"/> is null, otherwise to one class.
/// </summary>
public class Notice : TenantEntity
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Null = visible to the whole school.</summary>
    public Guid? SchoolClassId { get; set; }

    /// <summary>Hidden from parents after this date (null = never expires).</summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>Pinned notices sort to the top.</summary>
    public bool IsPinned { get; set; }
}
