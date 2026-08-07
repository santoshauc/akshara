namespace SchoolErp.AdminPortal.Models;

/// <summary>An active sign-in on some device (mirrors SessionDto).</summary>
public sealed record SessionDto(
    Guid Id,
    string? DeviceName,
    string? IpAddress,
    DateTimeOffset SignedInAt,
    DateTimeOffset LastRefreshedAt,
    DateTimeOffset ExpiresAt);
