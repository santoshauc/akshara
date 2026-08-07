using SchoolErp.Domain.Fees;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Fee head (mirrors FeeHeadDto).</summary>
public sealed record FeeHeadDto(Guid Id, string Name);

/// <summary>Installment line of a class plan (mirrors FeeStructureItemDto).</summary>
public sealed record FeeStructureItemDto(
    Guid Id, Guid FeeHeadId, string FeeHeadName, decimal Amount, DateOnly DueDate);

/// <summary>Input line when defining a plan (mirrors FeeStructureItemInput).</summary>
public sealed record FeeStructureItemInput(Guid FeeHeadId, decimal Amount, DateOnly DueDate);

/// <summary>Define-plan payload (mirrors DefineFeeStructureCommand).</summary>
public sealed record DefineFeeStructureRequest(
    Guid AcademicYearId, Guid SchoolClassId, List<FeeStructureItemInput> Items);

/// <summary>Due line (mirrors FeeDueLineDto).</summary>
public sealed record FeeDueLineDto(string FeeHeadName, decimal Amount, DateOnly DueDate, bool Overdue);

/// <summary>Payment row (mirrors FeePaymentDto).</summary>
public sealed record FeePaymentDto(
    Guid Id, string ReceiptNumber, decimal Amount, DateOnly PaidOn, PaymentMode Mode, string? Reference);

/// <summary>Student ledger (mirrors StudentFeeSummaryDto).</summary>
public sealed record StudentFeeSummaryDto(
    Guid StudentId,
    Guid AcademicYearId,
    List<FeeDueLineDto> DueLines,
    List<FeePaymentDto> Payments,
    decimal TotalDue,
    decimal TotalPaid,
    decimal Balance);

/// <summary>Record-payment payload (mirrors RecordPaymentCommand).</summary>
public sealed record RecordPaymentRequest(
    Guid StudentId,
    Guid AcademicYearId,
    decimal Amount,
    DateOnly PaidOn,
    PaymentMode Mode,
    string? Reference,
    string? Remarks);

/// <summary>Receipt (mirrors PaymentReceiptDto).</summary>
public sealed record PaymentReceiptDto(Guid PaymentId, string ReceiptNumber, decimal Amount);
