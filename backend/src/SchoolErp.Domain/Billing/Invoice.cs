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

    /// <summary>
    /// Denormalized sum of the lines PLUS tax; kept in step on every write.
    /// Pre-GST rows carry zero tax, so their totals still read exactly as issued.
    /// </summary>
    public decimal TotalAmount { get; set; }

    // GST is FROZEN onto the row at issue time, never derived from live
    // configuration or the tenant afterwards. An invoice is a legal record: the
    // operator registering for GST next quarter, or a school correcting its
    // GSTIN, must not silently rewrite what last quarter's invoices say.

    /// <summary>The platform's GSTIN as configured when this was issued; null = plain invoice.</summary>
    public string? SupplierGstin { get; set; }

    /// <summary>The school's GSTIN at issue time, when it had one.</summary>
    public string? BuyerGstin { get; set; }

    /// <summary>State used for the CGST/SGST-vs-IGST decision, for the printed record.</summary>
    public string? PlaceOfSupply { get; set; }

    /// <summary>Services Accounting Code printed on the invoice.</summary>
    public string? SacCode { get; set; }

    /// <summary>Whole GST rate applied (e.g. 18). Zero on plain invoices.</summary>
    public decimal TaxRatePercent { get; set; }

    public decimal CgstAmount { get; set; }

    public decimal SgstAmount { get; set; }

    public decimal IgstAmount { get; set; }

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
