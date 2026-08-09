using SchoolErp.Domain.Fees;

namespace SchoolErp.AdminPortal.Models;

/// <summary>Fee head (mirrors FeeHeadDto).</summary>
public sealed record FeeHeadDto(
    Guid Id, string Name, LateFineType LateFineType, decimal LateFineValue);

/// <summary>Concession row (mirrors FeeConcessionDto).</summary>
public sealed record FeeConcessionDto(
    Guid Id, string? FeeHeadName, decimal Amount, string Reason);

/// <summary>Grant-concession payload (mirrors GrantConcessionCommand).</summary>
public sealed record GrantConcessionRequest(
    Guid StudentId, Guid AcademicYearId, Guid? FeeHeadId, decimal Amount, string Reason);

/// <summary>Installment line of a class plan (mirrors FeeStructureItemDto).</summary>
public sealed record FeeStructureItemDto(
    Guid Id, Guid FeeHeadId, string FeeHeadName, decimal Amount, DateOnly DueDate);

/// <summary>Input line when defining a plan (mirrors FeeStructureItemInput).</summary>
public sealed record FeeStructureItemInput(Guid FeeHeadId, decimal Amount, DateOnly DueDate);

/// <summary>Define-plan payload (mirrors DefineFeeStructureCommand).</summary>
public sealed record DefineFeeStructureRequest(
    Guid AcademicYearId, Guid SchoolClassId, List<FeeStructureItemInput> Items);

/// <summary>Due line (mirrors FeeDueLineDto).</summary>
public sealed record FeeDueLineDto(
    string FeeHeadName, decimal Amount, DateOnly DueDate, bool Overdue, decimal LateFine);

/// <summary>Payment row (mirrors FeePaymentDto).</summary>
public sealed record FeePaymentDto(
    Guid Id, string ReceiptNumber, decimal Amount, DateOnly PaidOn, PaymentMode Mode, string? Reference);

/// <summary>Student ledger (mirrors StudentFeeSummaryDto).</summary>
public sealed record StudentFeeSummaryDto(
    Guid StudentId,
    Guid AcademicYearId,
    List<FeeDueLineDto> DueLines,
    List<FeePaymentDto> Payments,
    List<FeeConcessionDto> Concessions,
    decimal TotalDue,
    decimal TotalLateFine,
    decimal TotalConcession,
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

/// <summary>One child's family-view line (mirrors FamilyChildFeeDto).</summary>
public sealed record FamilyChildFeeDto(
    Guid StudentId,
    string StudentName,
    string? ClassName,
    decimal TotalDue,
    decimal TotalConcession,
    decimal TotalPaid,
    decimal Balance);

/// <summary>Family ledger (mirrors FamilyFeeSummaryDto).</summary>
public sealed record FamilyFeeSummaryDto(
    List<FamilyChildFeeDto> Children, decimal FamilyBalance);
