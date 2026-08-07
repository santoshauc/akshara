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

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
