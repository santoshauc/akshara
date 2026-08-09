using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Billing;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.Billing;

/// <summary>One line as entered/shown.</summary>
public sealed record InvoiceLineDto(string Description, decimal Quantity, decimal UnitAmount, decimal Amount);

/// <summary>An invoice with its school's name for list views.</summary>
public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    Guid TenantId,
    string SchoolName,
    InvoiceStatus Status,
    DateOnly IssuedOn,
    DateOnly DueOn,
    DateOnly? PaidOn,
    decimal TotalAmount,
    string? Notes,
    IReadOnlyList<InvoiceLineDto> Lines);

/// <summary>Issues an invoice to a school. Lines carry quantity × unit price.</summary>
public sealed record CreateInvoiceCommand(
    Guid TenantId,
    DateOnly DueOn,
    IReadOnlyList<InvoiceLineDto> Lines,
    string? Notes) : IRequest<InvoiceDto>;

/// <summary>Shape rules: at least one priced line.</summary>
public sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(c => c.Lines).NotEmpty().WithMessage("An invoice needs at least one line.");
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description)
                .Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Line description is required.")
                .MaximumLength(300);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitAmount).GreaterThanOrEqualTo(0);
        });
        RuleFor(c => c.Notes).MaximumLength(1000);
    }
}

/// <summary>Creates the invoice with a sequential per-year number.</summary>
public sealed class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CreateInvoiceCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<InvoiceDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        // Sequential per-year number; the unique index arbitrates races (409 → retry).
        var prefix = $"INV-{today.Year}-";
        var count = await _db.Invoices
            .CountAsync(i => i.InvoiceNumber.StartsWith(prefix), cancellationToken)
            .ConfigureAwait(false);

        var invoice = new Invoice
        {
            TenantId = tenant.Id,
            InvoiceNumber = $"{prefix}{count + 1:D4}",
            IssuedOn = today,
            DueOn = request.DueOn,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Lines = request.Lines.Select(l => new InvoiceLine
            {
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitAmount = l.UnitAmount,
                Amount = Math.Round(l.Quantity * l.UnitAmount, 0),
            }).ToList(),
        };
        invoice.TotalAmount = invoice.Lines.Sum(l => l.Amount);
        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return invoice.ToDto(tenant.Name);
    }
}

/// <summary>Marks an issued invoice paid.</summary>
public sealed record MarkInvoicePaidCommand(Guid InvoiceId, DateOnly PaidOn) : IRequest;

/// <summary>Only issued invoices can be paid, exactly once.</summary>
public sealed class MarkInvoicePaidCommandHandler : IRequestHandler<MarkInvoicePaidCommand>
{
    private readonly IApplicationDbContext _db;

    public MarkInvoicePaidCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        if (invoice.Status != InvoiceStatus.Issued)
        {
            throw new ConflictException($"Invoice {invoice.InvoiceNumber} is already {invoice.Status}.");
        }

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidOn = request.PaidOn;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Voids an issued invoice (wrong amount, cancelled deal).</summary>
public sealed record VoidInvoiceCommand(Guid InvoiceId) : IRequest;

/// <summary>Paid invoices stay paid; only issued ones can be voided.</summary>
public sealed class VoidInvoiceCommandHandler : IRequestHandler<VoidInvoiceCommand>
{
    private readonly IApplicationDbContext _db;

    public VoidInvoiceCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(VoidInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        if (invoice.Status != InvoiceStatus.Issued)
        {
            throw new ConflictException($"Invoice {invoice.InvoiceNumber} is already {invoice.Status}.");
        }

        invoice.Status = InvoiceStatus.Void;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Sells an SMS pack: credits land on the school immediately and an issued
/// invoice records the receivable — one action, so credits and billing can
/// never drift apart.
/// </summary>
public sealed record RecordSmsTopUpCommand(
    Guid TenantId,
    int Credits,
    decimal UnitPrice,
    DateOnly DueOn) : IRequest<InvoiceDto>;

/// <summary>Pack shape rules.</summary>
public sealed class RecordSmsTopUpCommandValidator : AbstractValidator<RecordSmsTopUpCommand>
{
    public RecordSmsTopUpCommandValidator()
    {
        RuleFor(c => c.Credits).InclusiveBetween(500, 1_000_000);
        RuleFor(c => c.UnitPrice).InclusiveBetween(0.01m, 10m);
    }
}

/// <summary>Adds the credits and issues the invoice in one transaction.</summary>
public sealed class RecordSmsTopUpCommandHandler : IRequestHandler<RecordSmsTopUpCommand, InvoiceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;

    public RecordSmsTopUpCommandHandler(IApplicationDbContext db, ISender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<InvoiceDto> Handle(RecordSmsTopUpCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        var invoice = await _sender.Send(new CreateInvoiceCommand(
                tenant.Id,
                request.DueOn,
                [new InvoiceLineDto(
                    $"SMS top-up · {request.Credits:N0} credits",
                    request.Credits, request.UnitPrice, 0)],
                Notes: "Credits applied on issue."),
            cancellationToken).ConfigureAwait(false);

        tenant.SmsCredits += request.Credits;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return invoice;
    }
}

/// <summary>Invoices, newest first, optionally for one school.</summary>
public sealed record GetInvoicesQuery(Guid? TenantId) : IRequest<List<InvoiceDto>>;

/// <summary>Reads the ledger with school names joined in.</summary>
public sealed class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, List<InvoiceDto>>
{
    private readonly IApplicationDbContext _db;

    public GetInvoicesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<InvoiceDto>> Handle(
        GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .Where(i => request.TenantId == null || i.TenantId == request.TenantId)
            .OrderByDescending(i => i.InvoiceNumber)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = await _db.Tenants.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(i => i.ToDto(names.GetValueOrDefault(i.TenantId, "(unknown school)")))
            .ToList();
    }
}

/// <summary>Hand-written mapping.</summary>
public static class InvoiceMappings
{
    public static InvoiceDto ToDto(this Invoice invoice, string schoolName) => new(
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.TenantId,
        schoolName,
        invoice.Status,
        invoice.IssuedOn,
        invoice.DueOn,
        invoice.PaidOn,
        invoice.TotalAmount,
        invoice.Notes,
        invoice.Lines
            .Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitAmount, l.Amount))
            .ToList());
}
