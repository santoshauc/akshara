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

    /// <summary>Password was correct but a TOTP/recovery code must follow.</summary>
    MfaRequired,
    InvalidMfaCode,
}

/// <summary>Issued token pair. <c>ExpiresInSeconds</c> refers to the access token.</summary>
public sealed record AuthTokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>
/// Outcome of an authentication operation. <see cref="MfaChallenge"/> is a
/// short-lived token proving the password step succeeded; it must be
/// presented together with a TOTP or recovery code to finish signing in.
/// </summary>
public sealed record AuthResult(
    bool Succeeded, AuthError Error, AuthTokens? Tokens, string? MfaChallenge = null)
{
    public static AuthResult Success(AuthTokens tokens) => new(true, AuthError.None, tokens);

    public static AuthResult Fail(AuthError error) => new(false, error, null);

    public static AuthResult MfaRequired(string challenge) =>
        new(false, AuthError.MfaRequired, null, challenge);
}

/// <summary>Material the user needs to add SchoolErp to an authenticator app.</summary>
public sealed record MfaEnrollment(string SharedKey, string AuthenticatorUri);

/// <summary>One-time recovery codes, shown exactly once at enable time.</summary>
public sealed record MfaEnableResult(IReadOnlyList<string> RecoveryCodes);

/// <summary>One active sign-in (a live refresh-token chain on one device).</summary>
public sealed record SessionDto(
    Guid Id,
    string? DeviceName,
    string? IpAddress,
    DateTimeOffset SignedInAt,
    DateTimeOffset LastRefreshedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Authentication operations: password and OTP login, refresh-token rotation,
/// revocation, and per-device session management. Implemented in
/// Infrastructure over ASP.NET Identity.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Password login for staff/admin. <paramref name="login"/> is an email or
    /// phone; the school is identified by <paramref name="schoolCode"/>.
    /// Failed attempts count toward account lockout. <paramref name="deviceName"/>
    /// labels the session in "My devices".
    /// </summary>
    Task<AuthResult> LoginWithPasswordAsync(
        string schoolCode, string login, string password, string? ipAddress,
        string? deviceName = null, CancellationToken ct = default);

    /// <summary>Requests an OTP for a parent phone. Always returns silently — the caller
    /// must not be able to probe which phone numbers exist.</summary>
    Task RequestOtpAsync(string schoolCode, string phone, CancellationToken ct = default);

    /// <summary>Completes OTP login. Attempts are limited; codes are single-use.</summary>
    Task<AuthResult> LoginWithOtpAsync(
        string schoolCode, string phone, string code, string? ipAddress,
        string? deviceName = null, CancellationToken ct = default);

    /// <summary>
    /// Rotates a refresh token: the presented token is revoked and replaced.
    /// Presenting an already-revoked token is treated as theft — the whole
    /// token family for that user is revoked. Device identity carries across
    /// rotations so the session stays one row in "My devices".
    /// </summary>
    Task<AuthResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);

    /// <summary>Revokes a refresh token (logout).</summary>
    Task RevokeAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Finishes an MFA-gated login: the challenge from
    /// <see cref="AuthResult.MfaChallenge"/> plus a 6-digit TOTP code or a
    /// recovery code. Wrong codes count toward account lockout.
    /// </summary>
    Task<AuthResult> CompleteMfaLoginAsync(
        string challengeToken, string code, string? ipAddress,
        string? deviceName = null, CancellationToken ct = default);

    /// <summary>Whether the user has MFA turned on.</summary>
    Task<bool> IsMfaEnabledAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Begins (or restarts) authenticator enrollment for the user.</summary>
    Task<MfaEnrollment?> StartMfaEnrollmentAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Turns MFA on after the user proves the authenticator works.
    /// Returns null when the code is wrong.</summary>
    Task<MfaEnableResult?> EnableMfaAsync(Guid userId, string code, CancellationToken ct = default);

    /// <summary>Turns MFA off (requires a current TOTP code). Resets the key.</summary>
    Task<bool> DisableMfaAsync(Guid userId, string code, CancellationToken ct = default);

    /// <summary>Self-service password change. Returns null on success, else a
    /// human-readable reason (wrong current password, policy failure).</summary>
    Task<string?> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Starts a forgot-password flow: an OTP is sent to the account's
    /// phone. Always silent — callers must not learn which logins exist.</summary>
    Task RequestPasswordResetAsync(string schoolCode, string login, CancellationToken ct = default);

    /// <summary>Completes a forgot-password flow with the SMS code. Sessions
    /// are revoked on success.</summary>
    Task<bool> ResetForgottenPasswordAsync(
        string schoolCode, string login, string code, string newPassword,
        CancellationToken ct = default);

    /// <summary>The user's active sessions (devices), newest sign-in first.</summary>
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Revokes one of the user's own sessions ("sign out that device").
    /// Returns false when the session doesn't exist or belongs to someone else.
    /// </summary>
    Task<bool> RevokeSessionAsync(
        Guid userId, Guid sessionId, string? ipAddress, CancellationToken ct = default);
}
