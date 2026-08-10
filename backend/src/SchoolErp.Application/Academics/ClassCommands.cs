using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Academics;

namespace SchoolErp.Application.Academics;

/// <summary>Creates a class with its sections (e.g. "Grade 5" with A/B/C).</summary>
public sealed record CreateClassCommand(
    string Name,
    int DisplayOrder,
    IReadOnlyList<string> Sections,
    // Colleges group their cohorts under a programme ("B.Tech CSE →
    // Semester 3"); schools leave this null and nothing changes for them.
    Guid? ProgrammeId = null) : IRequest<SchoolClassDto>;

/// <summary>Class shape rules.</summary>
public sealed class CreateClassCommandValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Sections).NotEmpty()
            .WithMessage("At least one section is required.")
            .Must(s => s.Select(x => x.Trim().ToUpperInvariant()).Distinct().Count() == s.Count)
            .WithMessage("Section names must be unique.");
        RuleForEach(c => c.Sections).NotEmpty().MaximumLength(16);
    }
}

/// <summary>Creates the class + sections after a per-tenant name check.</summary>
public sealed class CreateClassCommandHandler : IRequestHandler<CreateClassCommand, SchoolClassDto>
{
    private readonly IApplicationDbContext _db;

    public CreateClassCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SchoolClassDto> Handle(CreateClassCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.SchoolClasses.AnyAsync(c => c.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"Class '{name}' already exists.");
        }

        if (request.ProgrammeId is { } programmeId)
        {
            var programme = await _db.Programmes
                .FirstOrDefaultAsync(p => p.Id == programmeId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("Programme", programmeId);
            if (!programme.IsActive)
            {
                throw new ConflictException(
                    $"Programme '{programme.Name}' is closed; it cannot take new cohorts.");
            }
        }

        var schoolClass = new SchoolClass
        {
            Name = name,
            DisplayOrder = request.DisplayOrder,
            ProgrammeId = request.ProgrammeId,
            Sections = request.Sections
                .Select(s => new Section { Name = s.Trim() })
                .ToList(),
        };
        _db.SchoolClasses.Add(schoolClass);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return schoolClass.ToDto();
    }
}

/// <summary>Lists classes with sections, in display order.</summary>
public sealed record GetClassesQuery : IRequest<IReadOnlyList<SchoolClassDto>>;

/// <summary>Simple projection query.</summary>
public sealed class GetClassesQueryHandler : IRequestHandler<GetClassesQuery, IReadOnlyList<SchoolClassDto>>
{
    private readonly IApplicationDbContext _db;

    public GetClassesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SchoolClassDto>> Handle(
        GetClassesQuery request, CancellationToken cancellationToken) =>
        await _db.SchoolClasses.AsNoTracking()
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(AcademicsMappings.ClassProjection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
