using SchoolErp.Domain.Transport;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Vehicle (mirrors VehicleDto).</summary>
public sealed record VehicleDto(
    Guid Id,
    string RegistrationNumber,
    string? Model,
    int Capacity,
    DateOnly? InsuranceExpiry,
    DateOnly? FitnessExpiry,
    VehicleStatus Status);

/// <summary>Create-vehicle payload (mirrors CreateVehicleCommand).</summary>
public sealed record CreateVehicleRequest(
    string RegistrationNumber,
    string? Model,
    int Capacity,
    DateOnly? InsuranceExpiry,
    DateOnly? FitnessExpiry);

/// <summary>Stop (mirrors RouteStopDto).</summary>
public sealed record RouteStopDto(
    Guid Id, string Name, int SortOrder, TimeOnly? PickupTime, decimal? Latitude, decimal? Longitude);

/// <summary>Stop input (mirrors RouteStopInput).</summary>
public sealed record RouteStopInput(
    string Name, TimeOnly? PickupTime, decimal? Latitude, decimal? Longitude);

/// <summary>Route (mirrors TransportRouteDto).</summary>
public sealed record TransportRouteDto(
    Guid Id,
    string Name,
    Guid? VehicleId,
    string? VehicleRegistration,
    string? DriverName,
    string? DriverPhone,
    int StudentCount,
    List<RouteStopDto> Stops);

/// <summary>Create-route payload (mirrors CreateRouteCommand).</summary>
public sealed record CreateRouteRequest(
    string Name,
    Guid? VehicleId,
    string? DriverName,
    string? DriverPhone,
    List<RouteStopInput> Stops);

/// <summary>Allocation payload (mirrors AssignStudentTransportCommand).</summary>
public sealed record AssignTransportRequest(Guid StudentId, Guid RouteId, Guid StopId);
