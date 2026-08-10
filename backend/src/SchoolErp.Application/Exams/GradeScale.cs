using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Exams;

namespace SchoolErp.Application.Exams;

/// <summary>One band of the institution's grading ordinance.</summary>
public sealed record GradeBandDto(decimal MinPercent, string Letter, int Point);

/// <summary>
/// The scale in force, and whether it is the institution's own or the UGC
/// fallback. The caller needs to know which: a scale nobody configured is a
/// setting worth reviewing, not a decision anyone made.
/// </summary>
public sealed record GradeScaleDto(IReadOnlyList<GradeBandDto> Bands, bool IsInstitutionDefined);

/// <summary>The grading ordinance for the caller's institution.</summary>
public sealed record GetGradeScaleQuery : IRequest<GradeScaleDto>;

/// <summary>
/// Replaces the whole ordinance. Whole-set replacement rather than per-band
/// edits: a scale is only coherent as a set, and editing one band at a time
/// leaves gaps and overlaps live between saves.
/// </summary>
public sealed record SetGradeScaleCommand(IReadOnlyList<GradeBandDto> Bands) : IRequest;

/// <summary>Shape rules for an ordinance.</summary>
public sealed class SetGradeScaleCommandValidator : AbstractValidator<SetGradeScaleCommand>
{
    public SetGradeScaleCommandValidator()
    {
        RuleForEach(c => c.Bands).ChildRules(band =>
        {
            band.RuleFor(b => b.MinPercent).InclusiveBetween(0m, 100m);
            band.RuleFor(b => b.Letter).NotEmpty().MaximumLength(4);
            band.RuleFor(b => b.Point).InclusiveBetween(0, 100);
        });

        RuleFor(c => c.Bands)
            .Must(bands => bands.Select(b => b.MinPercent).Distinct().Count() == bands.Count)
            .WithMessage("Two bands cannot start at the same percentage.")
            .When(c => c.Bands.Count > 0);

        // A scale that starts above zero leaves the marks beneath it ungraded,
        // and every result in that range silently becomes an F.
        RuleFor(c => c.Bands)
            .Must(bands => bands.Any(b => b.MinPercent == 0m) ||
                           bands.All(b => b.MinPercent > 0m))
            .When(c => c.Bands.Count > 0);
    }
}

/// <summary>Reads the ordinance, falling back to the UGC scale.</summary>
public sealed class GetGradeScaleQueryHandler : IRequestHandler<GetGradeScaleQuery, GradeScaleDto>
{
    private readonly IApplicationDbContext _db;

    public GetGradeScaleQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<GradeScaleDto> Handle(
        GetGradeScaleQuery request, CancellationToken cancellationToken)
    {
        var bands = await _db.GradeBands.AsNoTracking()
            .OrderByDescending(b => b.MinPercent)
            .Select(b => new GradeBandDto(b.MinPercent, b.Letter, b.Point))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return bands.Count > 0
            ? new GradeScaleDto(bands, IsInstitutionDefined: true)
            : new GradeScaleDto(
                CbcsGradeCalculator.UgcDefault
                    .Select(b => new GradeBandDto(b.MinPercent, b.Letter, b.Point))
                    .ToList(),
                IsInstitutionDefined: false);
    }
}

/// <summary>Replaces the ordinance wholesale.</summary>
public sealed class SetGradeScaleCommandHandler : IRequestHandler<SetGradeScaleCommand>
{
    private readonly IApplicationDbContext _db;

    public SetGradeScaleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetGradeScaleCommand request, CancellationToken cancellationToken)
    {
        var existing = await _db.GradeBands.ToListAsync(cancellationToken).ConfigureAwait(false);
        _db.GradeBands.RemoveRange(existing);

        // Results already published were computed against the OLD scale and
        // are not recomputed here — a transcript that changes after the fact
        // is worse than one that is out of date. Reissuing is a deliberate act.
        foreach (var band in request.Bands)
        {
            var letter = band.Letter.Trim();
            if (letter.Length == 0)
            {
                throw new ConflictException("A band needs a letter.");
            }

            _db.GradeBands.Add(new GradeBand
            {
                MinPercent = band.MinPercent,
                Letter = letter,
                Point = band.Point,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
