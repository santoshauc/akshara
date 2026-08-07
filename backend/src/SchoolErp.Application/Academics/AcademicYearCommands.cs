using AutoMapper;
using AutoMapper.QueryableExtensions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Academics;

namespace SchoolErp.Application.Academics;

/// <summary>Creates an academic session (e.g. "2026-27").</summary>
public sealed record CreateAcademicYearCommand(
    string Name, DateOnly StartDate, DateOnly EndDate, bool MakeCurrent) : IRequest<AcademicYearDto>;

/// <summary>Session shape rules.</summary>
public sealed class CreateAcademicYearCommandValidator : AbstractValidator<CreateAcademicYearCommand>
{
    public CreateAcademicYearCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(32);
        RuleFor(c => c.EndDate).GreaterThan(c => c.StartDate)
            .WithMessage("End date must be after the start date.");
    }
}

/// <summary>Creates the year; optionally flips the current-year flag.</summary>
public sealed class CreateAcademicYearCommandHandler
    : IRequestHandler<CreateAcademicYearCommand, AcademicYearDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;

    public CreateAcademicYearCommandHandler(IApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<AcademicYearDto> Handle(
        CreateAcademicYearCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.AcademicYears.AnyAsync(y => y.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"Academic year '{name}' already exists.");
        }

        if (request.MakeCurrent)
        {
            await ClearCurrentFlagAsync(_db, cancellationToken).ConfigureAwait(false);
        }

        var year = new AcademicYear
        {
            Name = name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = request.MakeCurrent,
        };
        _db.AcademicYears.Add(year);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return _mapper.Map<AcademicYearDto>(year);
    }

    /// <summary>Clears IsCurrent on tracked entities (never raw SQL — RLS-safe by construction).</summary>
    internal static async Task ClearCurrentFlagAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var current = await db.AcademicYears
            .Where(y => y.IsCurrent)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var year in current)
        {
            year.IsCurrent = false;
        }
    }
}

/// <summary>Marks a year as the current session.</summary>
public sealed record SetCurrentAcademicYearCommand(Guid Id) : IRequest;

/// <summary>Flips the current-year flag atomically.</summary>
public sealed class SetCurrentAcademicYearCommandHandler : IRequestHandler<SetCurrentAcademicYearCommand>
{
    private readonly IApplicationDbContext _db;

    public SetCurrentAcademicYearCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetCurrentAcademicYearCommand request, CancellationToken cancellationToken)
    {
        var year = await _db.AcademicYears
            .FirstOrDefaultAsync(y => y.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(AcademicYear), request.Id);

        await CreateAcademicYearCommandHandler.ClearCurrentFlagAsync(_db, cancellationToken)
            .ConfigureAwait(false);
        year.IsCurrent = true;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Lists academic sessions, newest first.</summary>
public sealed record GetAcademicYearsQuery : IRequest<IReadOnlyList<AcademicYearDto>>;

/// <summary>Simple projection query.</summary>
public sealed class GetAcademicYearsQueryHandler
    : IRequestHandler<GetAcademicYearsQuery, IReadOnlyList<AcademicYearDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;

    public GetAcademicYearsQueryHandler(IApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AcademicYearDto>> Handle(
        GetAcademicYearsQuery request, CancellationToken cancellationToken) =>
        await _db.AcademicYears.AsNoTracking()
            .OrderByDescending(y => y.StartDate)
            .ProjectTo<AcademicYearDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
