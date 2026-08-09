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
    /// <summary>
    /// Marks a platform token whose owner has not enabled MFA. Its presence
    /// denies the platform-only policy.
    /// </summary>
    public const string PlatformMfaSetupRequiredClaim = "platform_mfa_setup_required";

    /// <summary>
    /// <paramref name="schoolCode"/> is carried purely so the client can theme
    /// itself: sign-in no longer asks for a code, so without it the portal has
    /// no way to know which school's branding to fetch. Never used for
    /// authorization — the tenant GUID claim is the authority.
    /// </summary>
    public string CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        string? schoolCode = null)
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
            if (!string.IsNullOrWhiteSpace(schoolCode))
            {
                claims.Add(new Claim("school_code", schoolCode));
            }
        }
        else if (_options.RequirePlatformMfa && !user.TwoFactorEnabled)
        {
            // A platform account can edit every school and grant itself SMS
            // credits, so MFA is not optional. Refusing the sign-in outright
            // would brick an operator who has not enrolled yet — including the
            // first one, who has nowhere to enrol from. Instead they sign in,
            // this claim blocks every platform-only endpoint, and the Security
            // page (which is account-scoped, not platform-scoped) stays open
            // so they can turn MFA on and sign in again.
            claims.Add(new Claim(PlatformMfaSetupRequiredClaim, "true"));
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
