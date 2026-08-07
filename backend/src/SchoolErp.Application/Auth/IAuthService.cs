namespace SchoolErp.Application.Auth;

/// <summary>Why an authentication attempt failed.</summary>
public enum AuthError
{
    None = 0,
    SchoolNotFound,
    InvalidCredentials,
    LockedOut,
    UserInactive,
    InvalidToken,
    InvalidOtp,
    OtpExpired,
}

/// <summary>Issued token pair. <c>ExpiresInSeconds</c> refers to the access token.</summary>
public sealed record AuthTokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>Outcome of an authentication operation.</summary>
public sealed record AuthResult(bool Succeeded, AuthError Error, AuthTokens? Tokens)
{
    public static AuthResult Success(AuthTokens tokens) => new(true, AuthError.None, tokens);

    public static AuthResult Fail(AuthError error) => new(false, error, null);
}

/// <summary>
/// Authentication operations: password and OTP login, refresh-token rotation,
/// and revocation. Implemented in Infrastructure over ASP.NET Identity.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Password login for staff/admin. <paramref name="login"/> is an email or
    /// phone; the school is identified by <paramref name="schoolCode"/>.
    /// Failed attempts count toward account lockout.
    /// </summary>
    Task<AuthResult> LoginWithPasswordAsync(
        string schoolCode, string login, string password, string? ipAddress, CancellationToken ct = default);

    /// <summary>Requests an OTP for a parent phone. Always returns silently — the caller
    /// must not be able to probe which phone numbers exist.</summary>
    Task RequestOtpAsync(string schoolCode, string phone, CancellationToken ct = default);

    /// <summary>Completes OTP login. Attempts are limited; codes are single-use.</summary>
    Task<AuthResult> LoginWithOtpAsync(
        string schoolCode, string phone, string code, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Rotates a refresh token: the presented token is revoked and replaced.
    /// Presenting an already-revoked token is treated as theft — the whole
    /// token family for that user is revoked.
    /// </summary>
    Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);

    /// <summary>Revokes a refresh token (logout).</summary>
    Task RevokeAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);
}
