using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Campuses;

namespace SchoolErp.Application.Campuses;

/// <summary>A campus as lists and pickers show it.</summary>
public sealed record CampusDto(
    Guid Id,
    string Name,
    string Code,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactPhone,
    bool IsPrimary,
    bool IsActive);

/// <summary>Campuses of the caller's institution, primary first.</summary>
public sealed record GetCampusesQuery(bool IncludeInactive = false)
    : IRequest<IReadOnlyList<CampusDto>>;

/// <summary>Adds a campus. The first one becomes primary automatically.</summary>
public sealed record CreateCampusCommand(
    string Name,
    string Code,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactPhone) : IRequest<Guid>;

/// <summary>Edits a campus's details.</summary>
public sealed record UpdateCampusCommand(
    Guid Id,
    string Name,
    string Code,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? ContactPhone,
    bool IsActive) : IRequest;

/// <summary>Moves the primary flag to another campus.</summary>
public sealed record SetPrimaryCampusCommand(Guid Id) : IRequest;

/// <summary>Shape rules shared by create and update.</summary>
public sealed class CreateCampusCommandValidator : AbstractValidator<CreateCampusCommand>
{
    public CreateCampusCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(16);
        RuleFor(c => c.ContactPhone).MaximumLength(20);
    }
}

/// <inheritdoc cref="CreateCampusCommandValidator"/>
public sealed class UpdateCampusCommandValidator : AbstractValidator<UpdateCampusCommand>
{
    public UpdateCampusCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(16);
        RuleFor(c => c.ContactPhone).MaximumLength(20);
    }
}

/// <summary>Campus reads and writes; all tenant-scoped by RLS.</summary>
public sealed class CampusHandlers :
    IRequestHandler<GetCampusesQuery, IReadOnlyList<CampusDto>>,
    IRequestHandler<CreateCampusCommand, Guid>,
    IRequestHandler<UpdateCampusCommand>,
    IRequestHandler<SetPrimaryCampusCommand>
{
    private readonly IApplicationDbContext _db;

    public CampusHandlers(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CampusDto>> Handle(
        GetCampusesQuery request, CancellationToken cancellationToken) =>
        await _db.Campuses.AsNoTracking()
            .Where(c => request.IncludeInactive || c.IsActive)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.Name)
            .Select(c => new CampusDto(
                c.Id, c.Name, c.Code, c.AddressLine1, c.City, c.State,
                c.PostalCode, c.ContactPhone, c.IsPrimary, c.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Guid> Handle(
        CreateCampusCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Campuses.AnyAsync(c => c.Code == code, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"A campus with code '{code}' already exists.");
        }

        // The first campus is the primary one — an institution always has a
        // head location, and making the operator pick one is busywork.
        var isFirst = !await _db.Campuses.AnyAsync(cancellationToken).ConfigureAwait(false);

        var campus = new Campus
        {
            Name = request.Name.Trim(),
            Code = code,
            AddressLine1 = Clean(request.AddressLine1),
            City = Clean(request.City),
            State = Clean(request.State),
            PostalCode = Clean(request.PostalCode),
            ContactPhone = Clean(request.ContactPhone),
            IsPrimary = isFirst,
            IsActive = true,
        };

        _db.Campuses.Add(campus);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return campus.Id;
    }

    public async Task Handle(UpdateCampusCommand request, CancellationToken cancellationToken)
    {
        var campus = await _db.Campuses
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Campus), request.Id);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Campuses
                .AnyAsync(c => c.Code == code && c.Id != request.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"A campus with code '{code}' already exists.");
        }

        // Closing the head campus would leave the institution without one.
        if (!request.IsActive && campus.IsPrimary)
        {
            throw new ConflictException(
                "This is the primary campus. Make another campus primary before closing it.");
        }

        campus.Name = request.Name.Trim();
        campus.Code = code;
        campus.AddressLine1 = Clean(request.AddressLine1);
        campus.City = Clean(request.City);
        campus.State = Clean(request.State);
        campus.PostalCode = Clean(request.PostalCode);
        campus.ContactPhone = Clean(request.ContactPhone);
        campus.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task Handle(SetPrimaryCampusCommand request, CancellationToken cancellationToken)
    {
        var campuses = await _db.Campuses.ToListAsync(cancellationToken).ConfigureAwait(false);
        var target = campuses.FirstOrDefault(c => c.Id == request.Id)
            ?? throw new NotFoundException(nameof(Campus), request.Id);

        if (!target.IsActive)
        {
            throw new ConflictException("A closed campus cannot be the primary one.");
        }

        foreach (var campus in campuses)
        {
            campus.IsPrimary = campus.Id == target.Id;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
