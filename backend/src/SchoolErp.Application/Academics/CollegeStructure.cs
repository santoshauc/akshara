using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Academics;

namespace SchoolErp.Application.Academics;

/// <summary>A programme as lists and pickers show it.</summary>
public sealed record ProgrammeDto(
    Guid Id,
    Guid DepartmentId,
    string Name,
    string Code,
    ProgrammeLevel Level,
    int DurationYears,
    int TermsPerYear,
    bool IsActive,
    int Cohorts);

/// <summary>A department with the programmes it runs.</summary>
public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string Code,
    Guid? HeadTeacherId,
    string? HeadTeacherName,
    bool IsActive,
    IReadOnlyList<ProgrammeDto> Programmes);

/// <summary>The college's departments, each with its programmes.</summary>
public sealed record GetDepartmentsQuery(bool IncludeClosed = false)
    : IRequest<IReadOnlyList<DepartmentDto>>;

public sealed record CreateDepartmentCommand(string Name, string Code, Guid? HeadTeacherId)
    : IRequest<Guid>;

public sealed record UpdateDepartmentCommand(
    Guid Id, string Name, string Code, Guid? HeadTeacherId, bool IsActive) : IRequest;

public sealed record CreateProgrammeCommand(
    Guid DepartmentId,
    string Name,
    string Code,
    ProgrammeLevel Level,
    int DurationYears,
    int TermsPerYear) : IRequest<Guid>;

public sealed record UpdateProgrammeCommand(
    Guid Id,
    Guid DepartmentId,
    string Name,
    string Code,
    ProgrammeLevel Level,
    int DurationYears,
    int TermsPerYear,
    bool IsActive) : IRequest;

public sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(16);
    }
}

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(16);
    }
}

/// <summary>Shape rules shared by both programme commands.</summary>
public sealed class CreateProgrammeCommandValidator : AbstractValidator<CreateProgrammeCommand>
{
    public CreateProgrammeCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(16);
        RuleFor(c => c.DurationYears).InclusiveBetween(1, 10);
        RuleFor(c => c.TermsPerYear).InclusiveBetween(1, 4);
    }
}

/// <inheritdoc cref="CreateProgrammeCommandValidator"/>
public sealed class UpdateProgrammeCommandValidator : AbstractValidator<UpdateProgrammeCommand>
{
    public UpdateProgrammeCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(16);
        RuleFor(c => c.DurationYears).InclusiveBetween(1, 10);
        RuleFor(c => c.TermsPerYear).InclusiveBetween(1, 4);
    }
}

/// <summary>
/// Departments and programmes. Everything is tenant-scoped by RLS; nothing
/// here checks the institution type, because a school that never creates a
/// department simply has none — the portal is what hides the screens.
/// </summary>
public sealed class CollegeStructureHandlers :
    IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>,
    IRequestHandler<CreateDepartmentCommand, Guid>,
    IRequestHandler<UpdateDepartmentCommand>,
    IRequestHandler<CreateProgrammeCommand, Guid>,
    IRequestHandler<UpdateProgrammeCommand>
{
    private readonly IApplicationDbContext _db;

    public CollegeStructureHandlers(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DepartmentDto>> Handle(
        GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var departments = await _db.Departments
            .AsNoTracking()
            .Where(d => request.IncludeClosed || d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var programmes = await _db.Programmes
            .AsNoTracking()
            .Where(p => request.IncludeClosed || p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // How many cohorts each programme actually teaches, so a closed-looking
        // programme with live classes cannot be mistaken for an empty one.
        var cohorts = await _db.SchoolClasses
            .AsNoTracking()
            .Where(c => c.ProgrammeId != null)
            .GroupBy(c => c.ProgrammeId!.Value)
            .Select(g => new { ProgrammeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProgrammeId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        var headIds = departments
            .Where(d => d.HeadTeacherId is not null)
            .Select(d => d.HeadTeacherId!.Value)
            .Distinct()
            .ToList();
        var heads = headIds.Count == 0
            ? []
            : await _db.Teachers
                .AsNoTracking()
                .Where(t => headIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.FullName, cancellationToken)
                .ConfigureAwait(false);

        return departments
            .Select(d => new DepartmentDto(
                d.Id, d.Name, d.Code, d.HeadTeacherId,
                d.HeadTeacherId is { } head && heads.TryGetValue(head, out var name) ? name : null,
                d.IsActive,
                programmes
                    .Where(p => p.DepartmentId == d.Id)
                    .Select(p => new ProgrammeDto(
                        p.Id, p.DepartmentId, p.Name, p.Code, p.Level,
                        p.DurationYears, p.TermsPerYear, p.IsActive,
                        cohorts.TryGetValue(p.Id, out var count) ? count : 0))
                    .ToList()))
            .ToList();
    }

    public async Task<Guid> Handle(
        CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        await EnsureDepartmentIsUniqueAsync(code, name, null, cancellationToken).ConfigureAwait(false);
        await EnsureTeacherExistsAsync(request.HeadTeacherId, cancellationToken).ConfigureAwait(false);

        var department = new Department
        {
            Name = name,
            Code = code,
            HeadTeacherId = request.HeadTeacherId,
            IsActive = true,
        };

        _db.Departments.Add(department);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return department.Id;
    }

    public async Task Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Department), request.Id);

        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        await EnsureDepartmentIsUniqueAsync(code, name, request.Id, cancellationToken)
            .ConfigureAwait(false);
        await EnsureTeacherExistsAsync(request.HeadTeacherId, cancellationToken).ConfigureAwait(false);

        // Closing a department while it still runs something would orphan the
        // programmes underneath it in every picker.
        if (!request.IsActive && department.IsActive)
        {
            var live = await _db.Programmes
                .AnyAsync(p => p.DepartmentId == request.Id && p.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (live)
            {
                throw new ConflictException(
                    "This department still runs active programmes. Close those first.");
            }
        }

        department.Name = name;
        department.Code = code;
        department.HeadTeacherId = request.HeadTeacherId;
        department.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid> Handle(
        CreateProgrammeCommand request, CancellationToken cancellationToken)
    {
        var department = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == request.DepartmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Department), request.DepartmentId);

        if (!department.IsActive)
        {
            throw new ConflictException("That department is closed; reopen it first.");
        }

        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureProgrammeCodeIsFreeAsync(code, null, cancellationToken).ConfigureAwait(false);

        var programme = new Programme
        {
            DepartmentId = request.DepartmentId,
            Name = request.Name.Trim(),
            Code = code,
            Level = request.Level,
            DurationYears = request.DurationYears,
            TermsPerYear = request.TermsPerYear,
            IsActive = true,
        };

        _db.Programmes.Add(programme);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return programme.Id;
    }

    public async Task Handle(UpdateProgrammeCommand request, CancellationToken cancellationToken)
    {
        var programme = await _db.Programmes
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Programme), request.Id);

        var departmentExists = await _db.Departments
            .AnyAsync(d => d.Id == request.DepartmentId, cancellationToken)
            .ConfigureAwait(false);
        if (!departmentExists)
        {
            throw new NotFoundException(nameof(Department), request.DepartmentId);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureProgrammeCodeIsFreeAsync(code, request.Id, cancellationToken)
            .ConfigureAwait(false);

        programme.DepartmentId = request.DepartmentId;
        programme.Name = request.Name.Trim();
        programme.Code = code;
        programme.Level = request.Level;
        programme.DurationYears = request.DurationYears;
        programme.TermsPerYear = request.TermsPerYear;
        programme.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureDepartmentIsUniqueAsync(
        string code, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var clash = await _db.Departments
            .Where(d => d.Id != exceptId && (d.Code == code || d.Name == name))
            .Select(d => new { d.Code, d.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (clash is not null)
        {
            throw new ConflictException(clash.Code == code
                ? $"A department with code '{code}' already exists."
                : $"A department named '{name}' already exists.");
        }
    }

    private async Task EnsureProgrammeCodeIsFreeAsync(
        string code, Guid? exceptId, CancellationToken cancellationToken)
    {
        var taken = await _db.Programmes
            .AnyAsync(p => p.Id != exceptId && p.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (taken)
        {
            throw new ConflictException($"A programme with code '{code}' already exists.");
        }
    }

    /// <summary>A head of department has to be somebody on the staff list.</summary>
    private async Task EnsureTeacherExistsAsync(
        Guid? teacherId, CancellationToken cancellationToken)
    {
        if (teacherId is not { } id)
        {
            return;
        }

        var exists = await _db.Teachers
            .AnyAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
        {
            throw new NotFoundException("Teacher", id);
        }
    }
}
