using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Billing;

namespace SchoolErp.Application.Billing;

/// <summary>Everything the invoice PDF shows.</summary>
public sealed record InvoicePdfData(
    string InvoiceNumber,
    InvoiceStatus Status,
    string SchoolName,
    string? SchoolCity,
    DateOnly IssuedOn,
    DateOnly DueOn,
    DateOnly? PaidOn,
    decimal TotalAmount,
    string? Notes,
    IReadOnlyList<InvoiceLineDto> Lines,
    InvoiceTaxDto? Tax);

/// <summary>Renders the invoice PDF. Implemented in Infrastructure (QuestPDF).</summary>
public interface IInvoiceRenderer
{
    byte[] Render(InvoicePdfData data);
}

/// <summary>An invoice as a downloadable PDF.</summary>
public sealed record GetInvoicePdfQuery(Guid InvoiceId) : IRequest<byte[]>;

/// <summary>Loads the invoice and its school, then renders.</summary>
public sealed class GetInvoicePdfQueryHandler : IRequestHandler<GetInvoicePdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly IInvoiceRenderer _renderer;

    public GetInvoicePdfQueryHandler(IApplicationDbContext db, IInvoiceRenderer renderer)
    {
        _db = db;
        _renderer = renderer;
    }

    public async Task<byte[]> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        var school = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == invoice.TenantId)
            .Select(t => new { t.Name, t.City })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return _renderer.Render(new InvoicePdfData(
            invoice.InvoiceNumber,
            invoice.Status,
            school?.Name ?? "(unknown school)",
            school?.City,
            invoice.IssuedOn,
            invoice.DueOn,
            invoice.PaidOn,
            invoice.TotalAmount,
            invoice.Notes,
            invoice.Lines
                .Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitAmount, l.Amount))
                .ToList(),
            invoice.ToTaxDto()));
    }
}
