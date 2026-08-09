namespace SchoolErp.Infrastructure.Identity;

/// <summary>JWT issuance settings, bound from the <c>Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SchoolErp";

    public string Audience { get; set; } = "SchoolErp";

    /// <summary>
    /// HMAC-SHA256 signing key, minimum 32 bytes. Supplied via environment /
    /// secret manager in production — never committed.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether a platform (Super Admin) account must have MFA enabled before
    /// the platform screens will serve it. ON by default — an operator can
    /// edit every school. Development turns it off in appsettings so the
    /// seeded demo operator is usable without enrolling an authenticator,
    /// the same way the dev SMS and payment gateways stand in for real ones.
    /// </summary>
    public bool RequirePlatformMfa { get; set; } = true;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
