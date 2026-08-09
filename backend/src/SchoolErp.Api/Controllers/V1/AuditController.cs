using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Audit;
using SchoolErp.Application.Platform;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>The action audit trail (who did what, when, from where).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly ISender _sender;

    public AuditController(ISender sender) => _sender = sender;

    /// <summary>Latest audit entries for the caller's scope (max 200).</summary>
    [HttpGet]
    [HasPermission(Permissions.Audit.View)]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrail(
        [FromQuery] string? search,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct) =>
        Ok(await _sender.Send(new GetAuditTrailQuery(search, from, to), ct));
}

/// <summary>
/// The operator trail: what platform staff did to schools — plan changes, SMS
/// top-ups, invoices voided. Separate from <see cref="AuditController"/>
/// because that one is school-scoped and the tenant guard (rightly) refuses a
/// platform account there; without this endpoint the most powerful actions in
/// the system are recorded and unreadable.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/platform/audit")]
[PlatformOnly]
public sealed class PlatformAuditController : ControllerBase
{
    private readonly ISender _sender;

    public PlatformAuditController(ISender sender) => _sender = sender;

    /// <summary>
    /// Operator actions by default; pass <paramref name="schoolId"/> to read
    /// one school's trail instead (support, and itself an audited read only in
    /// the sense that the operator had to choose a school deliberately).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlatformTrail(
        [FromQuery] string? search,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? schoolId,
        CancellationToken ct) =>
        Ok(await _sender.Send(new GetAuditTrailQuery(search, from, to, schoolId), ct));
}

/// <summary>
/// The people who can administer every school. Managed here rather than in
/// <c>UsersController</c> because that one is a school's own staff list and
/// these accounts have no school.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/platform/operators")]
[PlatformOnly]
public sealed class PlatformOperatorsController : ControllerBase
{
    private readonly ISender _sender;

    public PlatformOperatorsController(ISender sender) => _sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformOperatorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperators(CancellationToken ct) =>
        Ok(await _sender.Send(new GetPlatformOperatorsQuery(), ct));

    /// <summary>Adds an operator with a temporary password.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOperator(
        [FromBody] CreateOperatorRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new CreatePlatformOperatorCommand(
            request.FullName, request.Email, request.TemporaryPassword), ct));

    /// <summary>Enables or disables an operator (disabling revokes sessions).</summary>
    [HttpPut("{operatorId:guid}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetActive(
        Guid operatorId, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        await _sender.Send(
            new SetPlatformOperatorActiveCommand(operatorId, request.IsActive), ct);
        return NoContent();
    }

    /// <summary>Sets a new temporary password and signs the operator out.</summary>
    [HttpPut("{operatorId:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        Guid operatorId, [FromBody] ResetOperatorPasswordRequest request, CancellationToken ct)
    {
        await _sender.Send(
            new ResetPlatformOperatorPasswordCommand(operatorId, request.NewPassword), ct);
        return NoContent();
    }
}

/// <summary>New-operator payload.</summary>
public sealed record CreateOperatorRequest(
    string FullName, string Email, string TemporaryPassword);

/// <summary>Enable/disable payload.</summary>
public sealed record SetActiveRequest(bool IsActive);

/// <summary>Operator password payload.</summary>
public sealed record ResetOperatorPasswordRequest(string NewPassword);
