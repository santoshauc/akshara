using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SchoolErp.Infrastructure.Identity;

/// <summary>
/// Creates signed access tokens and cryptographically random refresh tokens.
/// Access tokens embed the tenant and the user's effective permissions so
/// authorization needs no database round-trip.
/// </summary>
public sealed class JwtTokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured and at least 32 characters long.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public int AccessTokenSeconds => _options.AccessTokenMinutes * 60;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    /// <summary>Creates a signed access token for the user.</summary>
    public string CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
        var now = _clock.GetUtcNow();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
        };

        if (user.TenantId is { } tenantId)
        {
            claims.Add(new Claim("tenant", tenantId.ToString()));
        }

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Distinct(StringComparer.Ordinal)
            .Select(p => new Claim(Shared.Authorization.Permissions.ClaimType, p)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.UtcDateTime.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: _credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Short-lived proof that the password step of an MFA login succeeded.
    /// Carries no roles or permissions — it can only be exchanged, together
    /// with a valid TOTP/recovery code, for real tokens.
    /// </summary>
    public string CreateMfaChallengeToken(Guid userId)
    {
        var now = _clock.GetUtcNow();
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("purpose", "mfa"),
            ],
            notBefore: now.UtcDateTime,
            expires: now.UtcDateTime.AddMinutes(5),
            signingCredentials: _credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Returns the user id when the MFA challenge is genuine and fresh.</summary>
    public Guid? ValidateMfaChallengeToken(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = _credentials.Key,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);

            if (principal.FindFirst("purpose")?.Value != "mfa")
            {
                return null;
            }

            return Guid.TryParse(
                principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
                ? id
                : null;
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>256 bits of cryptographic randomness, Base64url-encoded.</summary>
    public static string GenerateRefreshToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>SHA-256 hash (Base64) used to store tokens and OTP codes at rest.</summary>
    public static string Hash(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
