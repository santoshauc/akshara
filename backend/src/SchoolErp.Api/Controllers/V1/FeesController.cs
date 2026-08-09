using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Application.Fees;
using SchoolErp.Application.Fees.Commands;
using SchoolErp.Application.Fees.Queries;
using SchoolErp.Infrastructure.Payments;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Fee management: heads, structures, collections, online orders.</summary>
[ApiController]
[RequiresModule(TenantModules.Fees)]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/fees")]
public sealed class FeesController : ControllerBase
{
    private readonly ISender _sender;

    public FeesController(ISender sender) => _sender = sender;

    /// <summary>Lists fee heads.</summary>
    [HttpGet("heads")]
    [HasPermission(Permissions.Fees.View)]
    [ProducesResponseType(typeof(IReadOnlyList<FeeHeadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHeads(CancellationToken ct) =>
        Ok(await _sender.Send(new GetFeeHeadsQuery(), ct));

    /// <summary>Creates a fee head.</summary>
    [HttpPost("heads")]
    [HasPermission(Permissions.Fees.Configure)]
    [ProducesResponseType(typeof(FeeHeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateHead(
        [FromBody] CreateFeeHeadCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Created($"api/v1/fees/heads/{result.Id}", result);
    }

    /// <summary>The fee plan for a class in a year.</summary>
    [HttpGet("structure")]
    [HasPermission(Permissions.Fees.View)]
    [ProducesResponseType(typeof(IReadOnlyList<FeeStructureItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStructure(
        [FromQuery] Guid academicYearId, [FromQuery] Guid classId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetFeeStructureQuery(academicYearId, classId), ct));

    /// <summary>Replaces the fee plan for a class in a year.</summary>
    [HttpPut("structure")]
    [HasPermission(Permissions.Fees.Configure)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DefineStructure(
        [FromBody] DefineFeeStructureCommand command, CancellationToken ct)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

    /// <summary>Records a manual payment (cash/cheque/direct UPI).</summary>
    [HttpPost("payments")]
    [HasPermission(Permissions.Fees.Collect)]
    [ProducesResponseType(typeof(PaymentReceiptDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordPaymentCommand command, CancellationToken ct)
    {
        var receipt = await _sender.Send(command, ct);
        return Created($"api/v1/fees/payments/{receipt.PaymentId}", receipt);
    }

    /// <summary>Creates an online payment order.</summary>
    [HttpPost("orders")]
    [HasPermission(Permissions.Fees.Collect)]
    [ProducesResponseType(typeof(PaymentOrderDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreatePaymentOrderCommand command, CancellationToken ct)
    {
        var order = await _sender.Send(command, ct);
        return Created($"api/v1/fees/orders/{order.OrderId}", order);
    }

    /// <summary>A student's fee ledger for a year.</summary>
    [HttpGet("students/{studentId:guid}")]
    [HasPermission(Permissions.Fees.View)]
    [ProducesResponseType(typeof(StudentFeeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentSummary(
        Guid studentId, [FromQuery] Guid academicYearId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentFeeSummaryQuery(studentId, academicYearId), ct));

    /// <summary>
    /// The family ledger from any sibling: guardian-linked students with
    /// their current-year balances and the combined family total.
    /// </summary>
    [HttpGet("students/{studentId:guid}/family")]
    [HasPermission(Permissions.Fees.View)]
    [ProducesResponseType(typeof(FamilyFeeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFamilyLedger(Guid studentId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentFamilyFeesQuery(studentId), ct));

    /// <summary>Grants a per-student concession.</summary>
    [HttpPost("concessions")]
    [HasPermission(Permissions.Fees.Configure)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantConcession(
        [FromBody] GrantConcessionCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));

    /// <summary>Withdraws a concession.</summary>
    [HttpDelete("concessions/{id:guid}")]
    [HasPermission(Permissions.Fees.Configure)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeConcession(Guid id, CancellationToken ct)
    {
        await _sender.Send(new RevokeConcessionCommand(id), ct);
        return NoContent();
    }

    /// <summary>A payment's receipt as a PDF.</summary>
    [HttpGet("payments/{id:guid}/receipt")]
    [HasPermission(Permissions.Fees.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken ct)
    {
        var pdf = await _sender.Send(new GetReceiptPdfQuery(id), ct);
        return File(pdf, "application/pdf", "receipt.pdf");
    }
}

/// <summary>
/// Gateway webhook receiver. Anonymous by design — authenticity comes from the
/// HMAC signature, verified before anything is touched.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/payments")]
public sealed class PaymentWebhooksController : ControllerBase
{
    private readonly GatewayWebhookProcessor _processor;

    public PaymentWebhooksController(GatewayWebhookProcessor processor) => _processor = processor;

    /// <summary>Receives gateway payment events.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Webhook-Signature"].FirstOrDefault() ?? string.Empty;

        var accepted = await _processor.ProcessAsync(body, signature, ct);
        return accepted ? Ok() : BadRequest();
    }
}
