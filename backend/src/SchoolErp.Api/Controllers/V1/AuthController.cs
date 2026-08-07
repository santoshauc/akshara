using System.ComponentModel.DataAnnotations;
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
            request.SchoolCode, request.Login, request.Password, ClientIp, ct);
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
            request.SchoolCode, request.Phone, request.Code, ClientIp, ct);
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

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private IActionResult ToActionResult(AuthResult result)
    {
        if (result.Succeeded)
        {
            return Ok(result.Tokens);
        }

        return result.Error switch
        {
            AuthError.LockedOut => Problem(
                title: "Account is temporarily locked. Try again later.",
                statusCode: StatusCodes.Status423Locked),
            AuthError.UserInactive => Problem(
                title: "This account has been deactivated.",
                statusCode: StatusCodes.Status403Forbidden),
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
