using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Billing;

/// <summary>Invoice lifecycle. Draft is deliberately absent — issue or don't.</summary>
public enum InvoiceStatus
{
    Issued = 1,
    Paid = 2,
    Void = 3,
}

/// <summary>
/// A platform invoice TO a school (licence, SMS top-ups, setup fees).
/// Platform-scoped like payment_orders — schools are the subject, the
/// platform operator is the audience — so no RLS and no tenant query filter;
/// every endpoint that touches it demands tenants.manage.
/// </summary>
public class Invoice : AuditableEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Sequential per year, e.g. "INV-2026-0001".</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    public DateOnly IssuedOn { get; set; }

    public DateOnly DueOn { get; set; }

    public DateOnly? PaidOn { get; set; }

    /// <summary>Denormalized sum of the lines; kept in step on every write.</summary>
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = [];
}

/// <summary>One priced line ("Annual licence 2026-27 · 850 students × ₹70").</summary>
public class InvoiceLine : AuditableEntity
{
    public Guid InvoiceId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    public decimal UnitAmount { get; set; }

    /// <summary>Quantity × unit, rounded to the rupee at write time.</summary>
    public decimal Amount { get; set; }
}
