using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Fees;

namespace SchoolErp.Application.Fees.Queries;

/// <summary>Everything a fee receipt shows, ready for rendering.</summary>
public sealed record ReceiptData(
    string SchoolName,
    string? SchoolCity,
    string ReceiptNumber,
    DateOnly PaidOn,
    string StudentName,
    string AdmissionNumber,
    string? ClassName,
    decimal Amount,
    PaymentMode Mode,
    string? Reference,
    decimal BalanceAfter);

/// <summary>Turns receipt data into a PDF. Implemented in Infrastructure.</summary>
public interface IReceiptRenderer
{
    byte[] Render(ReceiptData data);
}

/// <summary>A fee payment's receipt as a PDF.</summary>
public sealed record GetReceiptPdfQuery(Guid PaymentId) : IRequest<byte[]>;

/// <summary>Composes payment + student + running balance, then renders.</summary>
public sealed class GetReceiptPdfQueryHandler : IRequestHandler<GetReceiptPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ISender _sender;
    private readonly ITenantContext _tenantContext;
    private readonly IReceiptRenderer _renderer;

    public GetReceiptPdfQueryHandler(
        IApplicationDbContext db,
        ISender sender,
        ITenantContext tenantContext,
        IReceiptRenderer renderer)
    {
        _db = db;
        _sender = sender;
        _tenantContext = tenantContext;
        _renderer = renderer;
    }

    public async Task<byte[]> Handle(GetReceiptPdfQuery request, CancellationToken cancellationToken)
    {
        var payment = await _db.FeePayments.AsNoTracking()
            .Where(p => p.Id == request.PaymentId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(FeePayment), request.PaymentId);

        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == payment.StudentId)
            .Select(s => new
            {
                Name = (s.FirstName + " " + s.LastName).Trim(),
                s.AdmissionNumber,
            })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var className = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == payment.StudentId &&
                        e.AcademicYearId == payment.AcademicYearId)
            .Select(e => e.SchoolClass!.Name +
                (e.Section != null ? " " + e.Section.Name : ""))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var school = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => new { t.Name, t.City })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var summary = await _sender.Send(
            new GetStudentFeeSummaryQuery(payment.StudentId, payment.AcademicYearId),
            cancellationToken).ConfigureAwait(false);

        return _renderer.Render(new ReceiptData(
            school.Name,
            school.City,
            payment.ReceiptNumber,
            payment.PaidOn,
            student.Name,
            student.AdmissionNumber,
            className,
            payment.Amount,
            payment.Mode,
            payment.Reference,
            summary.Balance));
    }
}
