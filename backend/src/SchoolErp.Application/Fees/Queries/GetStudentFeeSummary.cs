using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Fees.Queries;

/// <summary>
/// A student's fee ledger for one academic year: the class plan's due lines,
/// payments received, and the running balance.
/// </summary>
public sealed record GetStudentFeeSummaryQuery(Guid StudentId, Guid AcademicYearId)
    : IRequest<StudentFeeSummaryDto>;

/// <summary>Composes dues (via the year's enrollment) and payments.</summary>
public sealed class GetStudentFeeSummaryQueryHandler
    : IRequestHandler<GetStudentFeeSummaryQuery, StudentFeeSummaryDto>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetStudentFeeSummaryQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<StudentFeeSummaryDto> Handle(
        GetStudentFeeSummaryQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == request.StudentId &&
                        e.AcademicYearId == request.AcademicYearId)
            .Select(e => new { e.SchoolClassId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Enrollment (student in this year)", request.StudentId);

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

        var planLines = await _db.FeeStructureItems.AsNoTracking()
            .Where(i => i.AcademicYearId == request.AcademicYearId &&
                        i.SchoolClassId == enrollment.SchoolClassId)
            .OrderBy(i => i.DueDate)
            .Select(i => new
            {
                HeadName = i.FeeHead!.Name,
                i.Amount,
                i.DueDate,
                i.FeeHead!.LateFineType,
                i.FeeHead!.LateFineValue,
                i.Label,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // A line past its due date accrues the head's fine once (flat INR or
        // percent of the line), rounded to the rupee.
        var dueLines = planLines.Select(line =>
        {
            var overdue = line.DueDate < today;
            var fine = overdue
                ? line.LateFineType switch
                {
                    Domain.Fees.LateFineType.Flat => line.LateFineValue,
                    Domain.Fees.LateFineType.Percent =>
                        Math.Round(line.Amount * line.LateFineValue / 100m,
                            0, MidpointRounding.AwayFromZero),
                    _ => 0m,
                }
                : 0m;
            return new FeeDueLineDto(line.HeadName, line.Amount, line.DueDate, overdue, fine, line.Label);
        }).ToList();

        var payments = await _db.FeePayments.AsNoTracking()
            .Where(p => p.StudentId == request.StudentId &&
                        p.AcademicYearId == request.AcademicYearId)
            .OrderByDescending(p => p.PaidOn)
            .Select(p => new FeePaymentDto(
                p.Id, p.ReceiptNumber, p.Amount, p.PaidOn, p.Mode, p.Reference))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var concessions = await _db.FeeConcessions.AsNoTracking()
            .Where(c => c.StudentId == request.StudentId &&
                        c.AcademicYearId == request.AcademicYearId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new FeeConcessionDto(
                c.Id, c.FeeHead != null ? c.FeeHead.Name : null, c.Amount, c.Reason))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalLateFine = dueLines.Sum(l => l.LateFine);
        var totalDue = dueLines.Sum(l => l.Amount) + totalLateFine;
        var totalConcession = concessions.Sum(c => c.Amount);
        var totalPaid = payments.Sum(p => p.Amount);

        return new StudentFeeSummaryDto
        {
            StudentId = request.StudentId,
            AcademicYearId = request.AcademicYearId,
            DueLines = dueLines,
            Payments = payments,
            Concessions = concessions,
            TotalDue = totalDue,
            TotalLateFine = totalLateFine,
            TotalConcession = totalConcession,
            TotalPaid = totalPaid,
            Balance = Math.Max(0, totalDue - totalConcession - totalPaid),
        };
    }
}
