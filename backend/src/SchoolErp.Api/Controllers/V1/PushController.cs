using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Notifications;
using SchoolErp.Infrastructure.Identity;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// Device push-token registration. Any signed-in user (parents in practice)
/// registers their device's Expo token after login; guardian-facing events
/// then push to every device of that phone number as well as sending SMS.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/push")]
[Authorize]
public sealed class PushController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public PushController(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    /// <summary>Registers (or refreshes) this device's push token.</summary>
    [HttpPost("tokens")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPushTokenRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userIdText ||
            !Guid.TryParse(userIdText, out var userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userIdText);
        if (user?.PhoneNumber is not { } phone)
        {
            return BadRequest("The account has no phone number to route pushes for.");
        }

        var existing = await _db.PushTokens
            .FirstOrDefaultAsync(t => t.Token == request.Token, ct);
        if (existing is null)
        {
            _db.PushTokens.Add(new PushToken
            {
                UserId = userId,
                Phone = phone,
                Token = request.Token.Trim(),
                Platform = request.Platform.Trim().ToLowerInvariant(),
            });
        }
        else
        {
            // The device changed hands (new login): re-point it.
            existing.UserId = userId;
            existing.Phone = phone;
            existing.Platform = request.Platform.Trim().ToLowerInvariant();
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Removes a device token (sign-out). Scoped to the caller's OWN tokens:
    /// an Expo token is not a secret, so without the ownership check anyone
    /// holding one could silence that parent's notifications.
    /// </summary>
    [HttpDelete("tokens")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unregister(
        [FromQuery][Required] string token, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } callerIdText ||
            !Guid.TryParse(callerIdText, out var callerId))
        {
            return Unauthorized();
        }

        var existing = await _db.PushTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.UserId == callerId, ct);
        if (existing is not null)
        {
            _db.PushTokens.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }

        return NoContent();
    }
}

/// <summary>Push token payload.</summary>
public sealed record RegisterPushTokenRequest(
    [Required][StringLength(256)] string Token,
    [Required][StringLength(16)] string Platform);
