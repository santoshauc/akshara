using SchoolErp.Domain.Common;
using SchoolErp.Domain.Students;

namespace SchoolErp.Domain.Hostel;

/// <summary>A hostel building with its warden contact.</summary>
public class HostelBuilding : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public string? WardenName { get; set; }

    /// <summary>E.164; shown to parents of boarders.</summary>
    public string? WardenPhone { get; set; }
}

/// <summary>A room within a hostel. Occupancy is capped by <see cref="Capacity"/>.</summary>
public class HostelRoom : TenantEntity
{
    public Guid HostelId { get; set; }

    public HostelBuilding? Hostel { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public int Capacity { get; set; }
}

/// <summary>
/// A student's stay in a room. Open allocations have a null
/// <see cref="VacatedOn"/>; a student holds at most one open allocation.
/// </summary>
public class HostelAllocation : TenantEntity
{
    public Guid RoomId { get; set; }

    public HostelRoom? Room { get; set; }

    public Guid StudentId { get; set; }

    public Student? Student { get; set; }

    public DateOnly AllocatedOn { get; set; }

    public DateOnly? VacatedOn { get; set; }
}
