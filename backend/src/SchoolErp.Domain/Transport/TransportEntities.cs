using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Transport;

public enum VehicleStatus
{
    Active = 1,
    Maintenance = 2,
    Retired = 3,
}

/// <summary>A school bus/van.</summary>
public class Vehicle : TenantEntity
{
    /// <summary>RTO registration ("TS09AB1234"), unique within the tenant.</summary>
    public string RegistrationNumber { get; set; } = string.Empty;

    public string? Model { get; set; }

    public int Capacity { get; set; }

    /// <summary>Expiry alerts surface on the transport dashboard.</summary>
    public DateOnly? InsuranceExpiry { get; set; }

    public DateOnly? FitnessExpiry { get; set; }

    public VehicleStatus Status { get; set; } = VehicleStatus.Active;
}

/// <summary>
/// A pickup/drop route. The driver link (<see cref="DriverUserId"/>) is what
/// the driver app authenticates against; name/phone are denormalized for the
/// parent app's "call the driver" surface.
/// </summary>
public class TransportRoute : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public string? DriverName { get; set; }

    /// <summary>E.164; also the driver-app OTP login identity.</summary>
    public string? DriverPhone { get; set; }

    /// <summary>Linked platform user for the driver app (set at provisioning).</summary>
    public Guid? DriverUserId { get; set; }

    public ICollection<RouteStop> Stops { get; set; } = [];
}

/// <summary>An ordered stop on a route.</summary>
public class RouteStop : TenantEntity
{
    public Guid RouteId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>1-based position along the route.</summary>
    public int SortOrder { get; set; }

    public TimeOnly? PickupTime { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }
}

/// <summary>A student's allocation to a route stop.</summary>
public class StudentTransportAssignment : TenantEntity
{
    public Guid StudentId { get; set; }

    public Guid RouteId { get; set; }

    public TransportRoute? Route { get; set; }

    public Guid StopId { get; set; }

    public RouteStop? Stop { get; set; }
}
