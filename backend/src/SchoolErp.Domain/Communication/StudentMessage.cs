using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Communication;

/// <summary>
/// One message in a student's parent↔school conversation. The thread IS the
/// student — no separate thread entity to manage. Read state is tracked per
/// side so both the parent app and the portal can badge unread counts.
/// </summary>
public class StudentMessage : TenantEntity
{
    public Guid StudentId { get; set; }

    /// <summary>True when school staff wrote it; false for the parent.</summary>
    public bool SentByStaff { get; set; }

    public Guid SenderUserId { get; set; }

    /// <summary>Display name snapshot (sender may be renamed/deleted later).</summary>
    public string SenderName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>When the parent side first saw it (staff messages only).</summary>
    public DateTimeOffset? ReadByParentAt { get; set; }

    /// <summary>When the school side first saw it (parent messages only).</summary>
    public DateTimeOffset? ReadByStaffAt { get; set; }
}
