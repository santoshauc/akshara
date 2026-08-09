namespace SchoolErp.AdminPortal.Models;

/// <summary>A campus as the register lists it.</summary>
public sealed record CampusDto(
    Guid Id,
    string Name,
    string Code,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactPhone,
    bool IsPrimary,
    bool IsActive);

/// <summary>Create payload; the first campus becomes primary server-side.</summary>
public sealed record CreateCampusRequest(
    string Name,
    string Code,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactPhone);

/// <summary>Edit payload; <see cref="IsActive"/> doubles as open/close.</summary>
public sealed record UpdateCampusRequest(
    string Name,
    string Code,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactPhone,
    bool IsActive);
