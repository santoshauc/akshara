using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Hostel;

namespace SchoolErp.Application.Hostel;

/// <summary>A hostel building with room/occupancy tallies.</summary>
public sealed record HostelDto(
    Guid Id,
    string Name,
    string? WardenName,
    string? WardenPhone,
    int RoomCount,
    int Capacity,
    int Occupied);

/// <summary>A room with live occupancy.</summary>
public sealed record HostelRoomDto(
    Guid Id,
    Guid HostelId,
    string RoomNumber,
    int Capacity,
    int Occupied);

/// <summary>One stay (open or historical).</summary>
public sealed record HostelAllocationDto(
    Guid Id,
    Guid RoomId,
    string RoomNumber,
    string HostelName,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    DateOnly AllocatedOn,
    DateOnly? VacatedOn);

/// <summary>Creates a hostel building.</summary>
public sealed record CreateHostelCommand(
    string Name, string? WardenName, string? WardenPhone) : IRequest<Guid>;

/// <summary>Hostel shape rules.</summary>
public sealed class CreateHostelCommandValidator : AbstractValidator<CreateHostelCommand>
{
    public CreateHostelCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(128);
        RuleFor(c => c.WardenName).MaximumLength(128);
        RuleFor(c => c.WardenPhone).Matches(@"^\+?[0-9]{10,15}$")
            .When(c => !string.IsNullOrWhiteSpace(c.WardenPhone))
            .WithMessage("Warden phone must be a valid 10–15 digit number.");
    }
}

/// <summary>Per-tenant unique name.</summary>
public sealed class CreateHostelCommandHandler : IRequestHandler<CreateHostelCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateHostelCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateHostelCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.Hostels.AnyAsync(h => h.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"Hostel '{name}' already exists.");
        }

        var hostel = new HostelBuilding
        {
            Name = name,
            WardenName = request.WardenName?.Trim(),
            WardenPhone = request.WardenPhone?.Trim(),
        };
        _db.Hostels.Add(hostel);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return hostel.Id;
    }
}

/// <summary>Adds a room to a hostel.</summary>
public sealed record AddHostelRoomCommand(Guid HostelId, string RoomNumber, int Capacity)
    : IRequest<Guid>;

/// <summary>Room shape rules.</summary>
public sealed class AddHostelRoomCommandValidator : AbstractValidator<AddHostelRoomCommand>
{
    public AddHostelRoomCommandValidator()
    {
        RuleFor(c => c.RoomNumber).NotEmpty().MaximumLength(16);
        RuleFor(c => c.Capacity).InclusiveBetween(1, 20);
    }
}

/// <summary>Room numbers are unique within a hostel.</summary>
public sealed class AddHostelRoomCommandHandler : IRequestHandler<AddHostelRoomCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public AddHostelRoomCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(AddHostelRoomCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Hostels.AnyAsync(h => h.Id == request.HostelId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(HostelBuilding), request.HostelId);
        }

        var number = request.RoomNumber.Trim();
        if (await _db.HostelRooms.AnyAsync(
                r => r.HostelId == request.HostelId && r.RoomNumber == number, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new ConflictException($"Room '{number}' already exists in this hostel.");
        }

        var room = new HostelRoom
        {
            HostelId = request.HostelId,
            RoomNumber = number,
            Capacity = request.Capacity,
        };
        _db.HostelRooms.Add(room);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return room.Id;
    }
}

/// <summary>Hostels with room/occupancy tallies.</summary>
public sealed record GetHostelsQuery : IRequest<IReadOnlyList<HostelDto>>;

/// <summary>Counts are computed in the database.</summary>
public sealed class GetHostelsQueryHandler : IRequestHandler<GetHostelsQuery, IReadOnlyList<HostelDto>>
{
    private readonly IApplicationDbContext _db;

    public GetHostelsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<HostelDto>> Handle(
        GetHostelsQuery request, CancellationToken cancellationToken) =>
        await _db.Hostels.AsNoTracking()
            .OrderBy(h => h.Name)
            .Select(h => new HostelDto(
                h.Id,
                h.Name,
                h.WardenName,
                h.WardenPhone,
                _db.HostelRooms.Count(r => r.HostelId == h.Id),
                _db.HostelRooms.Where(r => r.HostelId == h.Id).Sum(r => (int?)r.Capacity) ?? 0,
                _db.HostelAllocations.Count(a =>
                    a.VacatedOn == null && a.Room!.HostelId == h.Id)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>Rooms of one hostel with live occupancy.</summary>
public sealed record GetHostelRoomsQuery(Guid HostelId) : IRequest<IReadOnlyList<HostelRoomDto>>;

/// <summary>Ordered by room number.</summary>
public sealed class GetHostelRoomsQueryHandler
    : IRequestHandler<GetHostelRoomsQuery, IReadOnlyList<HostelRoomDto>>
{
    private readonly IApplicationDbContext _db;

    public GetHostelRoomsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<HostelRoomDto>> Handle(
        GetHostelRoomsQuery request, CancellationToken cancellationToken) =>
        await _db.HostelRooms.AsNoTracking()
            .Where(r => r.HostelId == request.HostelId)
            .OrderBy(r => r.RoomNumber)
            .Select(r => new HostelRoomDto(
                r.Id,
                r.HostelId,
                r.RoomNumber,
                r.Capacity,
                _db.HostelAllocations.Count(a => a.RoomId == r.Id && a.VacatedOn == null)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>Moves a student into a room.</summary>
public sealed record AllocateHostelRoomCommand(Guid RoomId, Guid StudentId) : IRequest<Guid>;

/// <summary>
/// Capacity and single-stay rules: the room must have a free bed and the
/// student must not already be living in any room.
/// </summary>
public sealed class AllocateHostelRoomCommandHandler
    : IRequestHandler<AllocateHostelRoomCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public AllocateHostelRoomCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(
        AllocateHostelRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _db.HostelRooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(HostelRoom), request.RoomId);

        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Student", request.StudentId);
        }

        if (await _db.HostelAllocations.AnyAsync(
                a => a.StudentId == request.StudentId && a.VacatedOn == null, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new ConflictException("The student already has a hostel room. Vacate it first.");
        }

        var occupied = await _db.HostelAllocations
            .CountAsync(a => a.RoomId == room.Id && a.VacatedOn == null, cancellationToken)
            .ConfigureAwait(false);
        if (occupied >= room.Capacity)
        {
            throw new ConflictException($"Room '{room.RoomNumber}' is full ({room.Capacity} beds).");
        }

        var allocation = new HostelAllocation
        {
            RoomId = room.Id,
            StudentId = request.StudentId,
            AllocatedOn = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime),
        };
        _db.HostelAllocations.Add(allocation);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return allocation.Id;
    }
}

/// <summary>Ends a stay.</summary>
public sealed record VacateHostelRoomCommand(Guid AllocationId) : IRequest;

/// <summary>Vacating a closed stay is a 409.</summary>
public sealed class VacateHostelRoomCommandHandler : IRequestHandler<VacateHostelRoomCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public VacateHostelRoomCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Handle(VacateHostelRoomCommand request, CancellationToken cancellationToken)
    {
        var allocation = await _db.HostelAllocations
            .FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(HostelAllocation), request.AllocationId);

        if (allocation.VacatedOn is not null)
        {
            throw new ConflictException("This stay is already closed.");
        }

        allocation.VacatedOn = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>A child's hostel stay as shown to parents.</summary>
public sealed record ChildHostelDto(
    string HostelName,
    string RoomNumber,
    string? WardenName,
    string? WardenPhone,
    DateOnly AllocatedOn);

/// <summary>The child's open stay; null when the student is a day scholar.</summary>
public sealed record GetStudentHostelQuery(Guid StudentId) : IRequest<ChildHostelDto?>;

/// <summary>Single lookup through the open allocation.</summary>
public sealed class GetStudentHostelQueryHandler
    : IRequestHandler<GetStudentHostelQuery, ChildHostelDto?>
{
    private readonly IApplicationDbContext _db;

    public GetStudentHostelQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ChildHostelDto?> Handle(
        GetStudentHostelQuery request, CancellationToken cancellationToken) =>
        await _db.HostelAllocations.AsNoTracking()
            .Where(a => a.StudentId == request.StudentId && a.VacatedOn == null)
            .Select(a => new ChildHostelDto(
                a.Room!.Hostel!.Name,
                a.Room.RoomNumber,
                a.Room.Hostel.WardenName,
                a.Room.Hostel.WardenPhone,
                a.AllocatedOn))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>Open stays (whole school or one room).</summary>
public sealed record GetHostelAllocationsQuery(Guid? RoomId = null)
    : IRequest<IReadOnlyList<HostelAllocationDto>>;

/// <summary>Ordered by hostel, room, student.</summary>
public sealed class GetHostelAllocationsQueryHandler
    : IRequestHandler<GetHostelAllocationsQuery, IReadOnlyList<HostelAllocationDto>>
{
    private readonly IApplicationDbContext _db;

    public GetHostelAllocationsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<HostelAllocationDto>> Handle(
        GetHostelAllocationsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.HostelAllocations.AsNoTracking().Where(a => a.VacatedOn == null);
        if (request.RoomId is { } roomId)
        {
            query = query.Where(a => a.RoomId == roomId);
        }

        return await query
            .OrderBy(a => a.Room!.Hostel!.Name).ThenBy(a => a.Room!.RoomNumber)
            .Select(a => new HostelAllocationDto(
                a.Id,
                a.RoomId,
                a.Room!.RoomNumber,
                a.Room.Hostel!.Name,
                a.StudentId,
                (a.Student!.FirstName + " " + a.Student.LastName).Trim(),
                a.Student.AdmissionNumber,
                a.AllocatedOn,
                a.VacatedOn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
