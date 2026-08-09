using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Billing;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.Billing;

/// <summary>The school's own subscription: plan, modules, credits, invoices.</summary>
public sealed record MySubscriptionDto(
    SubscriptionPlan Plan,
    DateOnly? ExpiresOn,
    IReadOnlyList<string> EnabledModules,
    int SmsCredits,
    decimal OutstandingTotal,
    IReadOnlyList<InvoiceDto> Invoices);

/// <summary>The signed-in school's subscription view (self-serve).</summary>
public sealed record GetMySubscriptionQuery : IRequest<MySubscriptionDto>;

/// <summary>Everything is scoped to the caller's tenant — nothing to leak.</summary>
public sealed class GetMySubscriptionQueryHandler
    : IRequestHandler<GetMySubscriptionQuery, MySubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetMySubscriptionQueryHandler(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<MySubscriptionDto> Handle(
        GetMySubscriptionQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var invoices = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.InvoiceNumber)
            .Take(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var modules = Enum.GetValues<TenantModules>()
            .Where(m => m != TenantModules.None && tenant.EnabledModules.HasFlag(m))
            .Select(m => m.ToString())
            .ToList();

        return new MySubscriptionDto(
            tenant.Plan,
            tenant.SubscriptionExpiresOn,
            modules,
            tenant.SmsCredits,
            invoices.Where(i => i.Status == InvoiceStatus.Issued).Sum(i => i.TotalAmount),
            invoices.Select(i => i.ToDto(tenant.Name)).ToList());
    }
}

/// <summary>
/// One of the school's OWN invoices as a PDF — 404 for anyone else's, so the
/// id alone can never cross a tenant boundary.
/// </summary>
public sealed record GetMyInvoicePdfQuery(Guid InvoiceId) : IRequest<byte[]>;

/// <summary>Ownership-checked wrapper over the renderer.</summary>
public sealed class GetMyInvoicePdfQueryHandler : IRequestHandler<GetMyInvoicePdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISender _sender;

    public GetMyInvoicePdfQueryHandler(
        IApplicationDbContext db, ITenantContext tenantContext, ISender sender)
    {
        _db = db;
        _tenantContext = tenantContext;
        _sender = sender;
    }

    public async Task<byte[]> Handle(
        GetMyInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var owned = await _db.Invoices.AsNoTracking()
            .AnyAsync(i => i.Id == request.InvoiceId && i.TenantId == _tenantContext.TenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!owned)
        {
            throw new NotFoundException(nameof(Invoice), request.InvoiceId);
        }

        return await _sender.Send(new GetInvoicePdfQuery(request.InvoiceId), cancellationToken)
            .ConfigureAwait(false);
    }
}
