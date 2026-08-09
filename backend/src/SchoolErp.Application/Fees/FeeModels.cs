using SchoolErp.Domain.Fees;

namespace SchoolErp.Application.Fees;

/// <summary>Fee head projection with its late-fine rule.</summary>
public sealed record FeeHeadDto(
    Guid Id, string Name, LateFineType LateFineType, decimal LateFineValue);

/// <summary>A per-student concession as shown on the ledger.</summary>
public sealed record FeeConcessionDto(
    Guid Id, string? FeeHeadName, decimal Amount, string Reason);

/// <summary>One installment line of a class fee plan.</summary>
public sealed record FeeStructureItemDto(
    Guid Id, Guid FeeHeadId, string FeeHeadName, decimal Amount, DateOnly DueDate,
    string? Label = null);

/// <summary>Input line when defining a class fee plan.</summary>
public sealed record FeeStructureItemInput(
    Guid FeeHeadId, decimal Amount, DateOnly DueDate, string? Label = null);

/// <summary>One due line in a student's summary. LateFine is 0 until overdue.</summary>
public sealed record FeeDueLineDto(
    string FeeHeadName, decimal Amount, DateOnly DueDate, bool Overdue,
    decimal LateFine = 0, string? Label = null);

/// <summary>One payment in a student's summary.</summary>
public sealed record FeePaymentDto(
    Guid Id,
    string ReceiptNumber,
    decimal Amount,
    DateOnly PaidOn,
    PaymentMode Mode,
    string? Reference);

/// <summary>A student's fee ledger for one academic year.</summary>
public sealed record StudentFeeSummaryDto
{
    public Guid StudentId { get; init; }
    public Guid AcademicYearId { get; init; }
    public IReadOnlyList<FeeDueLineDto> DueLines { get; init; } = [];
    public IReadOnlyList<FeePaymentDto> Payments { get; init; } = [];
    public IReadOnlyList<FeeConcessionDto> Concessions { get; init; } = [];

    /// <summary>Plan amounts plus accrued late fines.</summary>
    public decimal TotalDue { get; init; }
    public decimal TotalLateFine { get; init; }
    public decimal TotalConcession { get; init; }
    public decimal TotalPaid { get; init; }

    /// <summary>TotalDue − concessions − payments (never negative).</summary>
    public decimal Balance { get; init; }
}

/// <summary>Result of recording a payment.</summary>
public sealed record PaymentReceiptDto(Guid PaymentId, string ReceiptNumber, decimal Amount);

/// <summary>Result of creating an online payment order.</summary>
public sealed record PaymentOrderDto(Guid OrderId, string GatewayOrderId, decimal Amount);
