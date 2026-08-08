using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Fees;

/// <summary>How a fee head charges for late payment.</summary>
public enum LateFineType
{
    None = 0,
    /// <summary>Fixed INR added once a line is past its due date.</summary>
    Flat = 1,
    /// <summary>Percentage of the line amount added once past due.</summary>
    Percent = 2,
}

/// <summary>A chargeable category (Tuition, Transport, Lab…). Tenant-scoped.</summary>
public class FeeHead : TenantEntity
{
    /// <summary>Display name, unique within the tenant.</summary>
    public string Name { get; set; } = string.Empty;

    public LateFineType LateFineType { get; set; }

    /// <summary>INR for <see cref="LateFineType.Flat"/>; percent (0–100) for Percent.</summary>
    public decimal LateFineValue { get; set; }
}

/// <summary>
/// A per-student discount for one year — scholarship, sibling discount,
/// staff ward. A null fee head applies against the overall balance.
/// </summary>
public class FeeConcession : TenantEntity
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>Restricts the concession to one head; null = whole ledger.</summary>
    public Guid? FeeHeadId { get; set; }

    public FeeHead? FeeHead { get; set; }

    /// <summary>Flat INR off the balance.</summary>
    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// One installment line of a class's fee plan: "students of this class in this
/// year owe this amount under this head by this date". A class's full plan is
/// the set of its items.
/// </summary>
public class FeeStructureItem : TenantEntity
{
    public Guid AcademicYearId { get; set; }

    public Guid SchoolClassId { get; set; }

    public Guid FeeHeadId { get; set; }

    public FeeHead? FeeHead { get; set; }

    /// <summary>Amount in INR.</summary>
    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }
}

public enum PaymentMode
{
    Cash = 1,
    Cheque = 2,
    /// <summary>UPI paid directly to the school (recorded manually).</summary>
    Upi = 3,
    /// <summary>Collected through the online payment gateway.</summary>
    Online = 4,
}

/// <summary>A received payment against a student's ledger.</summary>
public class FeePayment : TenantEntity
{
    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    /// <summary>Sequential per-tenant receipt number ("RCP-2026-0001").</summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly PaidOn { get; set; }

    public PaymentMode Mode { get; set; }

    /// <summary>Cheque number, UPI reference, or gateway payment id.</summary>
    public string? Reference { get; set; }

    public string? Remarks { get; set; }
}

public enum PaymentOrderStatus
{
    Created = 1,
    Paid = 2,
    Failed = 3,
}

/// <summary>
/// An online-payment intent handed to the gateway. Deliberately NOT a
/// <see cref="TenantEntity"/>: the gateway webhook arrives with no tenant
/// scope and must find the order by gateway id alone (same pattern as the
/// outbox); the explicit <see cref="TenantId"/> restores the scope afterwards.
/// </summary>
public class PaymentOrder : AuditableEntity
{
    public Guid TenantId { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Gateway-issued order id (e.g. Razorpay order_xxx).</summary>
    public string GatewayOrderId { get; set; } = string.Empty;

    public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.Created;

    /// <summary>Gateway payment id once paid.</summary>
    public string? GatewayPaymentId { get; set; }
}
