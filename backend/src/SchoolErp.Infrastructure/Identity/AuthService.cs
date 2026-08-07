using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Auth;
using SchoolErp.Domain.Auth;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Infrastructure.Identity;

/// <summary>
/// ASP.NET Identity-backed implementation of <see cref="IAuthService"/>:
/// password + OTP login, rotating refresh tokens with reuse detection, and
/// account lockout.
/// </summary>
public sealed partial class AuthService : IAuthService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITenantLookup _tenantLookup;
    private readonly JwtTokenService _tokenService;
    private readonly ISmsSender _smsSender;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ITenantLookup tenantLookup,
        JwtTokenService tokenService,
        ISmsSender smsSender,
        TimeProvider clock,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _db = db;
        _tenantLookup = tenantLookup;
        _tokenService = tokenService;
        _smsSender = smsSender;
        _clock = clock;
        _logger = logger;
    }

    // ----- Password login --------------------------------------------------

    public async Task<AuthResult> LoginWithPasswordAsync(
        string schoolCode, string login, string password, string? ipAddress, CancellationToken ct = default)
    {
        Guid? tenantId = null;

        // An empty school code means platform sign-in (Super Admin — no tenant).
        if (!string.IsNullOrWhiteSpace(schoolCode))
        {
            var tenant = await _tenantLookup.FindByCodeAsync(schoolCode, ct).ConfigureAwait(false);
            if (tenant is null || !tenant.IsActive)
            {
                return AuthResult.Fail(AuthError.SchoolNotFound);
            }

            tenantId = tenant.Id;
        }

        var user = await FindTenantUserAsync(tenantId, login, ct).ConfigureAwait(false);
        if (user is null)
        {
            // Burn comparable time so missing users are indistinguishable
            // from wrong passwords.
            _ = new PasswordHasher<ApplicationUser>().HashPassword(null!, password);
            return AuthResult.Fail(AuthError.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return AuthResult.Fail(AuthError.UserInactive);
        }

        if (await _userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return AuthResult.Fail(AuthError.LockedOut);
        }

        if (!await _userManager.CheckPasswordAsync(user, password).ConfigureAwait(false))
        {
            await _userManager.AccessFailedAsync(user).ConfigureAwait(false);
            LogFailedLogin(_logger, user.Id);
            return await _userManager.IsLockedOutAsync(user).ConfigureAwait(false)
                ? AuthResult.Fail(AuthError.LockedOut)
                : AuthResult.Fail(AuthError.InvalidCredentials);
        }

        await _userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        return AuthResult.Success(await IssueTokensAsync(user, ipAddress, ct).ConfigureAwait(false));
    }

    // ----- OTP login -------------------------------------------------------

    public async Task RequestOtpAsync(string schoolCode, string phone, CancellationToken ct = default)
    {
        var tenant = await _tenantLookup.FindByCodeAsync(schoolCode, ct).ConfigureAwait(false);
        if (tenant is null || !tenant.IsActive)
        {
            return; // Silent: callers must not learn which schools/phones exist.
        }

        var user = await FindTenantUserByPhoneAsync(tenant.Id, phone, ct).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var now = _clock.GetUtcNow();

        // Throttle: at most 3 codes per phone per 15 minutes.
        var recent = await _db.Set<OtpCode>()
            .IgnoreQueryFilters()
            .CountAsync(o => o.TenantId == tenant.Id && o.Phone == phone &&
                             o.CreatedAt > now.AddMinutes(-15), ct)
            .ConfigureAwait(false);
        if (recent >= 3)
        {
            LogOtpThrottled(_logger, tenant.Id);
            return;
        }

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

        _db.Set<OtpCode>().Add(new OtpCode
        {
            TenantId = tenant.Id,
            Phone = phone,
            CodeHash = JwtTokenService.Hash(code),
            ExpiresAt = now.Add(OtpLifetime),
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        await _smsSender.SendAsync(
            phone,
            $"{code} is your {tenant.Name} login code. Valid for 5 minutes. Do not share it.",
            ct).ConfigureAwait(false);
    }

    public async Task<AuthResult> LoginWithOtpAsync(
        string schoolCode, string phone, string code, string? ipAddress, CancellationToken ct = default)
    {
        var tenant = await _tenantLookup.FindByCodeAsync(schoolCode, ct).ConfigureAwait(false);
        if (tenant is null || !tenant.IsActive)
        {
            return AuthResult.Fail(AuthError.SchoolNotFound);
        }

        var now = _clock.GetUtcNow();
        var otp = await _db.Set<OtpCode>()
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenant.Id && o.Phone == phone && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (otp is null || !otp.IsUsable(now))
        {
            return AuthResult.Fail(AuthError.OtpExpired);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(otp.CodeHash),
                Convert.FromBase64String(JwtTokenService.Hash(code))))
        {
            otp.Attempts++;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return AuthResult.Fail(AuthError.InvalidOtp);
        }

        otp.ConsumedAt = now;

        var user = await FindTenantUserByPhoneAsync(tenant.Id, phone, ct).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return AuthResult.Fail(AuthError.InvalidCredentials);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return AuthResult.Success(await IssueTokensAsync(user, ipAddress, ct).ConfigureAwait(false));
    }

    // ----- Refresh rotation ------------------------------------------------

    public async Task<AuthResult> RefreshAsync(
        string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var hash = JwtTokenService.Hash(refreshToken);

        var stored = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return AuthResult.Fail(AuthError.InvalidToken);
        }

        if (stored.RevokedAt is not null)
        {
            // A revoked token is being replayed — assume theft and kill the
            // entire family so the attacker's copy dies too.
            LogTokenReuse(_logger, stored.UserId);
            await RevokeAllActiveAsync(stored.UserId, ipAddress, "reuse-detected", now, ct)
                .ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return AuthResult.Fail(AuthError.InvalidToken);
        }

        if (now >= stored.ExpiresAt)
        {
            return AuthResult.Fail(AuthError.InvalidToken);
        }

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString()).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            return AuthResult.Fail(AuthError.UserInactive);
        }

        var newRawToken = JwtTokenService.GenerateRefreshToken();
        stored.RevokedAt = now;
        stored.RevokedByIp = ipAddress;
        stored.RevocationReason = "rotated";
        stored.ReplacedByTokenHash = JwtTokenService.Hash(newRawToken);

        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = stored.ReplacedByTokenHash,
            ExpiresAt = now.Add(_tokenService.RefreshTokenLifetime),
            CreatedByIp = ipAddress,
        });

        var accessToken = await CreateAccessTokenAsync(user).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return AuthResult.Success(
            new AuthTokens(accessToken, _tokenService.AccessTokenSeconds, newRawToken));
    }

    public async Task RevokeAsync(string refreshToken, string? ipAddress, CancellationToken ct = default)
    {
        var hash = JwtTokenService.Hash(refreshToken);
        var stored = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return;
        }

        stored.RevokedAt = _clock.GetUtcNow();
        stored.RevokedByIp = ipAddress;
        stored.RevocationReason = "logout";
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ----- Helpers ---------------------------------------------------------

    private async Task<AuthTokens> IssueTokensAsync(
        ApplicationUser user, string? ipAddress, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var rawToken = JwtTokenService.GenerateRefreshToken();

        _db.Set<RefreshToken>().Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtTokenService.Hash(rawToken),
            ExpiresAt = now.Add(_tokenService.RefreshTokenLifetime),
            CreatedByIp = ipAddress,
        });

        var accessToken = await CreateAccessTokenAsync(user).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new AuthTokens(accessToken, _tokenService.AccessTokenSeconds, rawToken);
    }

    /// <summary>Builds the access token with roles and effective permissions.</summary>
    private async Task<string> CreateAccessTokenAsync(ApplicationUser user)
    {
        var roleNames = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

        List<string> permissions;
        if (roleNames.Contains(WellKnownRoles.SuperAdmin, StringComparer.Ordinal))
        {
            permissions = Permissions.All.ToList();
        }
        else
        {
            permissions = await _db.Roles
                .Where(r => roleNames.Contains(r.Name!))
                .Join(_db.RoleClaims, r => r.Id, c => c.RoleId, (_, c) => c)
                .Where(c => c.ClaimType == Permissions.ClaimType && c.ClaimValue != null)
                .Select(c => c.ClaimValue!)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);
        }

        return _tokenService.CreateAccessToken(user, roleNames, permissions);
    }

    private Task<ApplicationUser?> FindTenantUserAsync(Guid? tenantId, string login, CancellationToken ct)
    {
        var normalized = login.Trim();
        var normalizedUpper = normalized.ToUpperInvariant();
        return _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantId &&
                 (u.NormalizedEmail == normalizedUpper || u.PhoneNumber == normalized),
            ct);
    }

    private Task<ApplicationUser?> FindTenantUserByPhoneAsync(Guid tenantId, string phone, CancellationToken ct)
    {
        var normalized = phone.Trim();
        return _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantId && u.PhoneNumber == normalized, ct);
    }

    private async Task RevokeAllActiveAsync(
        Guid userId, string? ipAddress, string reason, DateTimeOffset now, CancellationToken ct)
    {
        var active = await _db.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevokedByIp = ipAddress;
            token.RevocationReason = reason;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed login attempt for user {UserId}")]
    private static partial void LogFailedLogin(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OTP request throttled for tenant {TenantId}")]
    private static partial void LogOtpThrottled(ILogger logger, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Refresh-token reuse detected for user {UserId}; revoking token family")]
    private static partial void LogTokenReuse(ILogger logger, Guid userId);
}

/// <summary>Role names with platform-level semantics.</summary>
public static class WellKnownRoles
{
    /// <summary>Platform operator; implicitly holds every permission.</summary>
    public const string SuperAdmin = "SuperAdmin";

    public const string SchoolAdmin = "SchoolAdmin";
}
