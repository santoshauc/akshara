using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Auth;

/// <summary>
/// A one-time login code sent by SMS. Only the hash is stored. Platform-scoped
/// with an explicit <see cref="TenantId"/> column: OTP verification happens
/// during login, before a tenant scope exists.
/// </summary>
public class OtpCode : AuditableEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Phone number the code was sent to (E.164).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>SHA-256 hash (Base64) of the numeric code.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Failed verification attempts; the code dies after 5.</summary>
    public int Attempts { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public bool IsUsable(DateTimeOffset now) =>
        ConsumedAt is null && Attempts < 5 && now < ExpiresAt;
}
