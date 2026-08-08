using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SchoolErp.Application.Auth;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Authentication endpoints: password/OTP login, refresh, logout.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Password login for staff and administrators.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] PasswordLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginWithPasswordAsync(
            request.SchoolCode, request.Login, request.Password, ClientIp, DeviceLabel, ct);
        return ToActionResult(result);
    }

    /// <summary>Requests an SMS OTP for parent/driver login. Always returns 202.</summary>
    [HttpPost("otp/request")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequest request, CancellationToken ct)
    {
        await _authService.RequestOtpAsync(request.SchoolCode, request.Phone, ct);
        // Deliberately identical response whether or not the phone exists.
        return Accepted();
    }

    /// <summary>Completes OTP login.</summary>
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginWithOtpAsync(
            request.SchoolCode, request.Phone, request.Code, ClientIp, DeviceLabel, ct);
        return ToActionResult(result);
    }

    /// <summary>Rotates a refresh token for a new token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken, ClientIp, ct);
        return ToActionResult(result);
    }

    /// <summary>Revokes a refresh token (logout).</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _authService.RevokeAsync(request.RefreshToken, ClientIp, ct);
        return NoContent();
    }

    /// <summary>Self-service password change.</summary>
    [HttpPost("password/change")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        var error = await _authService.ChangePasswordAsync(
            userId, request.CurrentPassword, request.NewPassword, ct);
        return error is null
            ? NoContent()
            : Problem(title: error, statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>Starts a forgot-password flow (SMS code). Always 202.</summary>
    [HttpPost("password/forgot")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _authService.RequestPasswordResetAsync(request.SchoolCode, request.Login, ct);
        return Accepted();
    }

    /// <summary>Completes a forgot-password flow with the SMS code.</summary>
    [HttpPost("password/reset")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken ct) =>
        await _authService.ResetForgottenPasswordAsync(
            request.SchoolCode, request.Login, request.Code, request.NewPassword, ct)
            ? NoContent()
            : Problem(title: "The code is invalid or expired.",
                statusCode: StatusCodes.Status400BadRequest);

    /// <summary>Finishes an MFA-gated login with a TOTP or recovery code.</summary>
    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request, CancellationToken ct)
    {
        var result = await _authService.CompleteMfaLoginAsync(
            request.MfaToken, request.Code, ClientIp, DeviceLabel, ct);
        return ToActionResult(result);
    }

    /// <summary>Whether the caller has MFA turned on.</summary>
    [HttpGet("mfa")]
    [Authorize]
    [ProducesResponseType(typeof(MfaStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMfaStatus(CancellationToken ct) =>
        CurrentUserId is { } userId
            ? Ok(new MfaStatusResponse(await _authService.IsMfaEnabledAsync(userId, ct)))
            : Unauthorized();

    /// <summary>Starts authenticator enrollment: shared key + otpauth URI.</summary>
    [HttpPost("mfa/enroll")]
    [Authorize]
    [ProducesResponseType(typeof(MfaEnrollment), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnrollMfa(CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        var enrollment = await _authService.StartMfaEnrollmentAsync(userId, ct);
        return enrollment is null ? Unauthorized() : Ok(enrollment);
    }

    /// <summary>Turns MFA on once the authenticator produces a valid code.</summary>
    [HttpPost("mfa/enable")]
    [Authorize]
    [ProducesResponseType(typeof(MfaEnableResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnableMfa([FromBody] MfaCodeRequest request, CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _authService.EnableMfaAsync(userId, request.Code, ct);
        return result is null
            ? Problem(title: "That code didn't match. Check your authenticator app.",
                statusCode: StatusCodes.Status400BadRequest)
            : Ok(result);
    }

    /// <summary>Turns MFA off (requires a current code).</summary>
    [HttpPost("mfa/disable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DisableMfa([FromBody] MfaCodeRequest request, CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        return await _authService.DisableMfaAsync(userId, request.Code, ct)
            ? NoContent()
            : Problem(title: "That code didn't match. Check your authenticator app.",
                statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>The caller's active sessions ("My devices").</summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(CancellationToken ct) =>
        CurrentUserId is { } userId
            ? Ok(await _authService.GetSessionsAsync(userId, ct))
            : Unauthorized();

    /// <summary>Signs out one of the caller's own sessions.</summary>
    [HttpDelete("sessions/{sessionId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
        {
            return Unauthorized();
        }

        return await _authService.RevokeSessionAsync(userId, sessionId, ClientIp, ct)
            ? NoContent()
            : NotFound();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>"Chrome · Windows"-style label derived from the User-Agent.</summary>
    private string? DeviceLabel
    {
        get
        {
            var ua = Request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(ua))
            {
                return null;
            }

            var browser = ua switch
            {
                _ when ua.Contains("Edg/", StringComparison.Ordinal) => "Edge",
                _ when ua.Contains("OPR/", StringComparison.Ordinal) => "Opera",
                _ when ua.Contains("Chrome/", StringComparison.Ordinal) => "Chrome",
                _ when ua.Contains("Firefox/", StringComparison.Ordinal) => "Firefox",
                _ when ua.Contains("Safari/", StringComparison.Ordinal) => "Safari",
                _ when ua.Contains("okhttp", StringComparison.OrdinalIgnoreCase) => "Mobile app",
                _ => null,
            };
            var platform = ua switch
            {
                _ when ua.Contains("Android", StringComparison.Ordinal) => "Android",
                _ when ua.Contains("iPhone", StringComparison.Ordinal) ||
                       ua.Contains("iPad", StringComparison.Ordinal) => "iOS",
                _ when ua.Contains("Windows", StringComparison.Ordinal) => "Windows",
                _ when ua.Contains("Mac OS", StringComparison.Ordinal) => "macOS",
                _ when ua.Contains("Linux", StringComparison.Ordinal) => "Linux",
                _ => null,
            };

            var label = (browser, platform) switch
            {
                (null, null) => ua,
                (null, _) => platform!,
                (_, null) => browser!,
                _ => $"{browser} · {platform}",
            };
            return label.Length <= 128 ? label : label[..128];
        }
    }

    private IActionResult ToActionResult(AuthResult result)
    {
        if (result.Succeeded)
        {
            return Ok(result.Tokens);
        }

        if (result.Error == AuthError.MfaRequired)
        {
            // 200 with a challenge, not an error: the password was correct.
            return Ok(new MfaChallengeResponse(true, result.MfaChallenge!));
        }

        return result.Error switch
        {
            AuthError.LockedOut => Problem(
                title: "Account is temporarily locked. Try again later.",
                statusCode: StatusCodes.Status423Locked),
            AuthError.UserInactive => Problem(
                title: "This account has been deactivated.",
                statusCode: StatusCodes.Status403Forbidden),
            AuthError.SubscriptionExpired => Problem(
                title: "Your school's subscription has expired. " +
                    "Please ask the school to renew to restore access.",
                statusCode: StatusCodes.Status423Locked),
            _ => Problem(
                title: "Invalid credentials.",
                statusCode: StatusCodes.Status401Unauthorized),
        };
    }
}

/// <summary>Password login payload. Empty school code = platform (Super Admin) sign-in.</summary>
public sealed record PasswordLoginRequest(
    [StringLength(16)] string SchoolCode,
    [Required][StringLength(320)] string Login,
    [Required][StringLength(128)] string Password);

/// <summary>OTP request payload.</summary>
public sealed record OtpRequest(
    [Required][StringLength(16)] string SchoolCode,
    [Required][Phone][StringLength(20)] string Phone);

/// <summary>OTP verification payload.</summary>
public sealed record OtpVerifyRequest(
    [Required][StringLength(16)] string SchoolCode,
    [Required][Phone][StringLength(20)] string Phone,
    [Required][StringLength(6, MinimumLength = 6)] string Code);

/// <summary>Refresh/logout payload.</summary>
public sealed record RefreshRequest([Required] string RefreshToken);

/// <summary>Returned by login when a second factor must follow.</summary>
public sealed record MfaChallengeResponse(bool MfaRequired, string MfaToken);

/// <summary>Second step of an MFA login.</summary>
public sealed record MfaVerifyRequest(
    [Required] string MfaToken,
    [Required][StringLength(32)] string Code);

/// <summary>A TOTP code on its own (enable/disable).</summary>
public sealed record MfaCodeRequest([Required][StringLength(32)] string Code);

/// <summary>Caller's MFA state.</summary>
public sealed record MfaStatusResponse(bool Enabled);

/// <summary>Self-service password change payload.</summary>
public sealed record ChangePasswordRequest(
    [Required][StringLength(128)] string CurrentPassword,
    [Required][StringLength(128, MinimumLength = 8)] string NewPassword);

/// <summary>Forgot-password start payload.</summary>
public sealed record ForgotPasswordRequest(
    [Required][StringLength(16)] string SchoolCode,
    [Required][StringLength(320)] string Login);

/// <summary>Forgot-password completion payload.</summary>
public sealed record ResetPasswordRequest(
    [Required][StringLength(16)] string SchoolCode,
    [Required][StringLength(320)] string Login,
    [Required][StringLength(6, MinimumLength = 6)] string Code,
    [Required][StringLength(128, MinimumLength = 8)] string NewPassword);
