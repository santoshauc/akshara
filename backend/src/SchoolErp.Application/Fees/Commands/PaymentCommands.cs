using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Fees;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Students;
using SchoolErp.Shared.Localization;

namespace SchoolErp.Application.Fees.Commands;

/// <summary>
/// Records a manual payment (cash/cheque/direct UPI): issues the next
/// sequential receipt and queues a confirmation SMS to the primary guardian —
/// all in one transaction.
/// </summary>
public sealed record RecordPaymentCommand(
    Guid StudentId,
    Guid AcademicYearId,
    decimal Amount,
    DateOnly PaidOn,
    PaymentMode Mode,
    string? Reference,
    string? Remarks) : IRequest<PaymentReceiptDto>;

/// <summary>Payment shape rules.</summary>
public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(c => c.Amount).GreaterThan(0).LessThanOrEqualTo(10_00_000);
        RuleFor(c => c.Mode).IsInEnum().NotEqual(PaymentMode.Online)
            .WithMessage("Online payments are recorded via the gateway webhook, not manually.");
        RuleFor(c => c.Reference).MaximumLength(128);
        RuleFor(c => c.Remarks).MaximumLength(256);
    }
}

/// <summary>Creates the payment row and receipt.</summary>
public sealed class RecordPaymentCommandHandler
    : IRequestHandler<RecordPaymentCommand, PaymentReceiptDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLookup _tenantLookup;

    public RecordPaymentCommandHandler(
        IApplicationDbContext db, ITenantContext tenantContext, ITenantLookup tenantLookup)
    {
        _db = db;
        _tenantContext = tenantContext;
        _tenantLookup = tenantLookup;
    }

    public async Task<PaymentReceiptDto> Handle(
        RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        return await PaymentRecorder.RecordAsync(
            _db, _tenantContext, _tenantLookup,
            request.StudentId, request.AcademicYearId, request.Amount,
            request.PaidOn, request.Mode, request.Reference, request.Remarks,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Shared payment-recording core used by both the manual command and the
/// gateway-webhook path: student check, sequential receipt, outbox SMS.
/// </summary>
public static class PaymentRecorder
{
    public static async Task<PaymentReceiptDto> RecordAsync(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        ITenantLookup tenantLookup,
        Guid studentId,
        Guid academicYearId,
        decimal amount,
        DateOnly paidOn,
        PaymentMode mode,
        string? reference,
        string? remarks,
        CancellationToken ct)
    {
        var student = await db.Students
            .Where(s => s.Id == studentId)
            .Select(s => new { s.Id, s.FirstName })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), studentId);

        // Sequential per-year receipt; the unique index arbitrates races (409 → retry).
        var prefix = $"RCP-{paidOn.Year}-";
        var count = await db.FeePayments
            .CountAsync(p => p.ReceiptNumber.StartsWith(prefix), ct)
            .ConfigureAwait(false);
        var receiptNumber = $"{prefix}{count + 1:D4}";

        var payment = new FeePayment
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId,
            ReceiptNumber = receiptNumber,
            Amount = amount,
            PaidOn = paidOn,
            Mode = mode,
            Reference = reference,
            Remarks = remarks,
        };
        db.FeePayments.Add(payment);

        var guardianPhone = await db.StudentGuardians
            .Where(sg => sg.StudentId == student.Id && sg.IsPrimary && sg.Guardian != null)
            .Select(sg => sg.Guardian!.Phone)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (guardianPhone is not null)
        {
            var tenant = await tenantLookup.FindByIdAsync(tenantContext.TenantId, ct)
                .ConfigureAwait(false);
            await Notifications.NotificationQueue.QueueGuardianAsync(
                db, tenantContext.TenantId, guardianPhone,
                NotificationTemplates.PaymentReceived,
                [amount, student.FirstName, tenant?.Name ?? "your school", receiptNumber],
                ct).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return new PaymentReceiptDto(payment.Id, receiptNumber, amount);
    }
}

/// <summary>Creates an online payment order with the gateway.</summary>
public sealed record CreatePaymentOrderCommand(
    Guid StudentId, Guid AcademicYearId, decimal Amount) : IRequest<PaymentOrderDto>;

/// <summary>Order shape rules.</summary>
public sealed class CreatePaymentOrderCommandValidator : AbstractValidator<CreatePaymentOrderCommand>
{
    public CreatePaymentOrderCommandValidator()
    {
        RuleFor(c => c.Amount).GreaterThan(0).LessThanOrEqualTo(10_00_000);
    }
}

/// <summary>Creates the gateway order and the local intent row.</summary>
public sealed class CreatePaymentOrderCommandHandler
    : IRequestHandler<CreatePaymentOrderCommand, PaymentOrderDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPaymentGateway _gateway;

    public CreatePaymentOrderCommandHandler(
        IApplicationDbContext db, ITenantContext tenantContext, IPaymentGateway gateway)
    {
        _db = db;
        _tenantContext = tenantContext;
        _gateway = gateway;
    }

    public async Task<PaymentOrderDto> Handle(
        CreatePaymentOrderCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(Student), request.StudentId);
        }

        var gatewayOrderId = await _gateway
            .CreateOrderAsync(request.Amount, $"stu-{request.StudentId:N}", cancellationToken)
            .ConfigureAwait(false);

        var order = new PaymentOrder
        {
            TenantId = _tenantContext.TenantId,
            StudentId = request.StudentId,
            AcademicYearId = request.AcademicYearId,
            Amount = request.Amount,
            GatewayOrderId = gatewayOrderId,
        };
        _db.PaymentOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new PaymentOrderDto(order.Id, gatewayOrderId, request.Amount);
    }
}
