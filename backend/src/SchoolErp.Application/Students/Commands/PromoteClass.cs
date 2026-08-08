using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Students.Commands;

/// <summary>
/// Year-end promotion for one section: every Active enrollment in the source
/// year/class/section (minus the opt-out list) is closed as Promoted and a
/// fresh Active enrollment is created in the target year/class/section.
/// Excluded students are left untouched — promote them separately (repeat
/// year) or mark them Left/Completed by hand. Roll numbers do not carry over.
/// </summary>
public sealed record PromoteClassCommand(
    Guid FromAcademicYearId,
    Guid FromClassId,
    Guid FromSectionId,
    Guid ToAcademicYearId,
    Guid ToClassId,
    Guid ToSectionId,
    IReadOnlyList<Guid> ExcludedStudentIds) : IRequest<PromotionResult>;

/// <summary>What the promotion actually did.</summary>
public sealed record PromotionResult(int Promoted, int Excluded, int AlreadyEnrolled);

/// <summary>Promotion shape rules.</summary>
public sealed class PromoteClassCommandValidator : AbstractValidator<PromoteClassCommand>
{
    public PromoteClassCommandValidator()
    {
        RuleFor(c => c.ToAcademicYearId)
            .NotEqual(c => c.FromAcademicYearId)
            .WithMessage("Promotion must target a different academic year.");
    }
}

/// <summary>Runs the promotion in one transaction.</summary>
public sealed class PromoteClassCommandHandler
    : IRequestHandler<PromoteClassCommand, PromotionResult>
{
    private readonly IApplicationDbContext _db;

    public PromoteClassCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<PromotionResult> Handle(
        PromoteClassCommand request, CancellationToken cancellationToken)
    {
        _ = await _db.AcademicYears
                .FirstOrDefaultAsync(y => y.Id == request.ToAcademicYearId, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(AcademicYear), request.ToAcademicYearId);

        var targetSectionExists = await _db.Sections
            .AnyAsync(s => s.Id == request.ToSectionId && s.SchoolClassId == request.ToClassId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!targetSectionExists)
        {
            throw new NotFoundException(nameof(Section), request.ToSectionId);
        }

        var source = await _db.Enrollments
            .Where(e => e.AcademicYearId == request.FromAcademicYearId &&
                        e.SchoolClassId == request.FromClassId &&
                        e.SectionId == request.FromSectionId &&
                        e.Status == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Students already placed in the target year keep that placement —
        // promoting twice must be harmless, not a duplicate-enrollment factory.
        var studentIds = source.Select(e => e.StudentId).ToList();
        var alreadyEnrolled = await _db.Enrollments
            .Where(e => e.AcademicYearId == request.ToAcademicYearId &&
                        studentIds.Contains(e.StudentId))
            .Select(e => e.StudentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var excluded = request.ExcludedStudentIds.ToHashSet();
        var promoted = 0;
        foreach (var enrollment in source)
        {
            if (excluded.Contains(enrollment.StudentId) ||
                alreadyEnrolled.Contains(enrollment.StudentId))
            {
                continue;
            }

            enrollment.Status = EnrollmentStatus.Promoted;
            _db.Enrollments.Add(new Enrollment
            {
                StudentId = enrollment.StudentId,
                AcademicYearId = request.ToAcademicYearId,
                SchoolClassId = request.ToClassId,
                SectionId = request.ToSectionId,
                Status = EnrollmentStatus.Active,
            });
            promoted++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PromotionResult(
            promoted,
            source.Count(e => excluded.Contains(e.StudentId)),
            alreadyEnrolled.Count);
    }
}
