using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Admissions;
using SchoolErp.Domain.Admissions;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// The admissions enquiry pipeline: capture, work, and convert prospective
/// admissions into students.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admissions/enquiries")]
public sealed class AdmissionsController : ControllerBase
{
    private readonly ISender _sender;

    public AdmissionsController(ISender sender) => _sender = sender;

    /// <summary>The board: follow-ups due first; optional status filter.</summary>
    [HttpGet]
    [HasPermission(Permissions.Admissions.View)]
    [ProducesResponseType(typeof(IReadOnlyList<EnquiryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnquiries(
        [FromQuery] EnquiryStatus? status, CancellationToken ct) =>
        Ok(await _sender.Send(new GetEnquiriesQuery(status), ct));

    /// <summary>Registers a fresh enquiry.</summary>
    [HttpPost]
    [HasPermission(Permissions.Admissions.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEnquiryCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));

    /// <summary>Moves an enquiry through the pipeline (not to Admitted).</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Admissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateEnquiryRequest request, CancellationToken ct)
    {
        await _sender.Send(new UpdateEnquiryCommand(
            id, request.Status, request.FollowUpOn, request.Notes), ct);
        return NoContent();
    }

    /// <summary>Stamps the enquiry Admitted and links the created student.</summary>
    [HttpPost("{id:guid}/convert")]
    [HasPermission(Permissions.Admissions.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Convert(
        Guid id, [FromBody] ConvertEnquiryRequest request, CancellationToken ct)
    {
        await _sender.Send(new ConvertEnquiryCommand(id, request.StudentId), ct);
        return NoContent();
    }
}

/// <summary>Pipeline move payload.</summary>
public sealed record UpdateEnquiryRequest(
    EnquiryStatus Status, DateOnly? FollowUpOn, string? Notes);

/// <summary>Conversion payload: the student the admission created.</summary>
public sealed record ConvertEnquiryRequest(Guid StudentId);
