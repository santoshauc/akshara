using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Billing;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// The school's own subscription — the self-serve side of platform billing.
/// Everything is scoped to the caller's tenant.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/subscription")]
public sealed class SubscriptionController : ControllerBase
{
    private readonly ISender _sender;

    public SubscriptionController(ISender sender) => _sender = sender;

    /// <summary>Plan, modules, SMS credits and the school's invoices.</summary>
    [HttpGet]
    [HasPermission(Permissions.Subscription.View)]
    [ProducesResponseType(typeof(MySubscriptionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(await _sender.Send(new GetMySubscriptionQuery(), ct));

    /// <summary>One of the school's own invoices as a PDF.</summary>
    [HttpGet("invoices/{id:guid}/pdf")]
    [HasPermission(Permissions.Subscription.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct) =>
        File(await _sender.Send(new GetMyInvoicePdfQuery(id), ct),
            "application/pdf", "invoice.pdf");
}
