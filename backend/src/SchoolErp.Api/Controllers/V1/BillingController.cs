using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Billing;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// Platform billing: what schools owe Akshara. Everything here is the
/// operator's view — tenants.manage throughout.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[PlatformOnly]
[Route("api/v{version:apiVersion}/billing")]
public sealed class BillingController : ControllerBase
{
    private readonly ISender _sender;

    public BillingController(ISender sender) => _sender = sender;

    /// <summary>What one school consumes: students, SMS, collections, dues.</summary>
    [HttpGet("tenants/{tenantId:guid}/usage")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(typeof(TenantUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsage(Guid tenantId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTenantUsageQuery(tenantId), ct));

    /// <summary>Invoices, newest first; optionally one school's.</summary>
    [HttpGet("invoices")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] Guid? tenantId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetInvoicesQuery(tenantId), ct));

    /// <summary>Issues an invoice.</summary>
    [HttpPost("invoices")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateInvoiceCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));

    /// <summary>Marks an issued invoice paid.</summary>
    [HttpPost("invoices/{id:guid}/paid")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkPaid(
        Guid id, [FromBody] MarkPaidRequest request, CancellationToken ct)
    {
        await _sender.Send(new MarkInvoicePaidCommand(id, request.PaidOn), ct);
        return NoContent();
    }

    /// <summary>Voids an issued invoice.</summary>
    [HttpPost("invoices/{id:guid}/void")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Void(Guid id, CancellationToken ct)
    {
        await _sender.Send(new VoidInvoiceCommand(id), ct);
        return NoContent();
    }

    /// <summary>The invoice as a PDF.</summary>
    [HttpGet("invoices/{id:guid}/pdf")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct) =>
        File(await _sender.Send(new GetInvoicePdfQuery(id), ct),
            "application/pdf", "invoice.pdf");

    /// <summary>
    /// Sells an SMS pack: credits are applied to the school immediately and
    /// an issued invoice records the receivable.
    /// </summary>
    [HttpPost("tenants/{tenantId:guid}/sms-topup")]
    [HasPermission(Permissions.TenantCatalog.Manage)]
    [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SmsTopUp(
        Guid tenantId, [FromBody] SmsTopUpRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new RecordSmsTopUpCommand(
            tenantId, request.Credits, request.UnitPrice, request.DueOn), ct));
}

/// <summary>Mark-paid payload.</summary>
public sealed record MarkPaidRequest(DateOnly PaidOn);

/// <summary>SMS pack payload.</summary>
public sealed record SmsTopUpRequest(int Credits, decimal UnitPrice, DateOnly DueOn);
