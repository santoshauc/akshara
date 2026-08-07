using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Transport;

public enum TripType
{
    Pickup = 1,
    Drop = 2,
}

public enum TripStatus
{
    InProgress = 1,
    Completed = 2,
}

/// <summary>
/// One bus run (pickup or drop). Cannot be created unless the driver
/// completed the pre-trip inspection — the checklist gate lives in the
/// start command.
/// </summary>
public class Trip : TenantEntity
{
    public Guid RouteId { get; set; }

    public TransportRoute? Route { get; set; }

    public TripType Type { get; set; }

    public TripStatus Status { get; set; } = TripStatus.InProgress;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Pre-trip checklist confirmation (fuel/tyres/brakes/emergency kit).</summary>
    public bool InspectionOk { get; set; }

    public string? InspectionNotes { get; set; }
}

/// <summary>A GPS ping from the driver app during a trip.</summary>
public class TripLocation : TenantEntity
{
    public Guid TripId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public DateTimeOffset RecordedAt { get; set; }
}

public enum TripStudentEventType
{
    PickedUp = 1,
    Dropped = 2,
    /// <summary>Student not at the stop.</summary>
    Absent = 3,
}

/// <summary>A per-student event during a trip (board/alight/no-show).</summary>
public class TripStudentEvent : TenantEntity
{
    public Guid TripId { get; set; }

    public Guid StudentId { get; set; }

    public TripStudentEventType EventType { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string? Remarks { get; set; }
}
