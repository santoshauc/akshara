using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Fees;

namespace SchoolErp.Application.Fees.Commands;

/// <summary>Creates a fee head (Tuition, Transport…).</summary>
public sealed record CreateFeeHeadCommand(string Name) : IRequest<FeeHeadDto>;

/// <summary>Fee-head shape rules.</summary>
public sealed class CreateFeeHeadCommandValidator : AbstractValidator<CreateFeeHeadCommand>
{
    public CreateFeeHeadCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
    }
}

/// <summary>Creates the head after a per-tenant uniqueness check.</summary>
public sealed class CreateFeeHeadCommandHandler : IRequestHandler<CreateFeeHeadCommand, FeeHeadDto>
{
    private readonly IApplicationDbContext _db;

    public CreateFeeHeadCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<FeeHeadDto> Handle(CreateFeeHeadCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.FeeHeads.AnyAsync(h => h.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"Fee head '{name}' already exists.");
        }

        var head = new FeeHead { Name = name };
        _db.FeeHeads.Add(head);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new FeeHeadDto(head.Id, head.Name);
    }
}

/// <summary>Lists fee heads.</summary>
public sealed record GetFeeHeadsQuery : IRequest<IReadOnlyList<FeeHeadDto>>;

/// <summary>Simple projection query.</summary>
public sealed class GetFeeHeadsQueryHandler : IRequestHandler<GetFeeHeadsQuery, IReadOnlyList<FeeHeadDto>>
{
    private readonly IApplicationDbContext _db;

    public GetFeeHeadsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeeHeadDto>> Handle(
        GetFeeHeadsQuery request, CancellationToken cancellationToken) =>
        await _db.FeeHeads.AsNoTracking()
            .OrderBy(h => h.Name)
            .Select(h => new FeeHeadDto(h.Id, h.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>
/// Replaces the fee plan for a class in a year with the given installment
/// lines. Full-replace keeps the operation idempotent and the UI simple.
/// </summary>
public sealed record DefineFeeStructureCommand(
    Guid AcademicYearId,
    Guid SchoolClassId,
    IReadOnlyList<FeeStructureItemInput> Items) : IRequest;

/// <summary>Structure shape rules.</summary>
public sealed class DefineFeeStructureCommandValidator : AbstractValidator<DefineFeeStructureCommand>
{
    public DefineFeeStructureCommandValidator()
    {
        RuleFor(c => c.Items).NotEmpty()
            .Must(items => items
                .Select(i => (i.FeeHeadId, i.DueDate))
                .Distinct()
                .Count() == items.Count)
            .WithMessage("A fee head may appear only once per due date.");

        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Amount).GreaterThan(0).LessThanOrEqualTo(10_00_000);
        });
    }
}

/// <summary>Validates references then replaces the plan atomically.</summary>
public sealed class DefineFeeStructureCommandHandler : IRequestHandler<DefineFeeStructureCommand>
{
    private readonly IApplicationDbContext _db;

    public DefineFeeStructureCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DefineFeeStructureCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.AcademicYears.AnyAsync(y => y.Id == request.AcademicYearId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("AcademicYear", request.AcademicYearId);
        }

        if (!await _db.SchoolClasses.AnyAsync(c => c.Id == request.SchoolClassId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("SchoolClass", request.SchoolClassId);
        }

        var headIds = request.Items.Select(i => i.FeeHeadId).Distinct().ToList();
        var known = await _db.FeeHeads
            .Where(h => headIds.Contains(h.Id))
            .Select(h => h.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var missing = headIds.Except(known).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException("FeeHead", missing[0]);
        }

        var existing = await _db.FeeStructureItems
            .Where(i => i.AcademicYearId == request.AcademicYearId &&
                        i.SchoolClassId == request.SchoolClassId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.FeeStructureItems.RemoveRange(existing);

        foreach (var item in request.Items)
        {
            _db.FeeStructureItems.Add(new FeeStructureItem
            {
                AcademicYearId = request.AcademicYearId,
                SchoolClassId = request.SchoolClassId,
                FeeHeadId = item.FeeHeadId,
                Amount = item.Amount,
                DueDate = item.DueDate,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>The fee plan for a class in a year.</summary>
public sealed record GetFeeStructureQuery(Guid AcademicYearId, Guid SchoolClassId)
    : IRequest<IReadOnlyList<FeeStructureItemDto>>;

/// <summary>Simple projection query.</summary>
public sealed class GetFeeStructureQueryHandler
    : IRequestHandler<GetFeeStructureQuery, IReadOnlyList<FeeStructureItemDto>>
{
    private readonly IApplicationDbContext _db;

    public GetFeeStructureQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeeStructureItemDto>> Handle(
        GetFeeStructureQuery request, CancellationToken cancellationToken) =>
        await _db.FeeStructureItems.AsNoTracking()
            .Where(i => i.AcademicYearId == request.AcademicYearId &&
                        i.SchoolClassId == request.SchoolClassId)
            .OrderBy(i => i.DueDate).ThenBy(i => i.FeeHead!.Name)
            .Select(i => new FeeStructureItemDto(
                i.Id, i.FeeHeadId, i.FeeHead!.Name, i.Amount, i.DueDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
