using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.Transport;

namespace SchoolErp.Application.Transport;

/// <summary>Vehicle projection.</summary>
public sealed record VehicleDto(
    Guid Id,
    string RegistrationNumber,
    string? Model,
    int Capacity,
    DateOnly? InsuranceExpiry,
    DateOnly? FitnessExpiry,
    VehicleStatus Status);

/// <summary>Stop projection.</summary>
public sealed record RouteStopDto(
    Guid Id, string Name, int SortOrder, TimeOnly? PickupTime, decimal? Latitude, decimal? Longitude);

/// <summary>Stop input when defining a route.</summary>
public sealed record RouteStopInput(
    string Name, TimeOnly? PickupTime, decimal? Latitude, decimal? Longitude);

/// <summary>Route projection with stops and student count.</summary>
public sealed record TransportRouteDto(
    Guid Id,
    string Name,
    Guid? VehicleId,
    string? VehicleRegistration,
    string? DriverName,
    string? DriverPhone,
    int StudentCount,
    IReadOnlyList<RouteStopDto> Stops);

/// <summary>A child's transport info for the parent app.</summary>
public sealed record ChildTransportDto(
    string RouteName,
    string StopName,
    TimeOnly? PickupTime,
    string? DriverName,
    string? DriverPhone,
    string? VehicleRegistration);

/// <summary>Registers a vehicle.</summary>
public sealed record CreateVehicleCommand(
    string RegistrationNumber,
    string? Model,
    int Capacity,
    DateOnly? InsuranceExpiry,
    DateOnly? FitnessExpiry) : IRequest<VehicleDto>;

/// <summary>Vehicle shape rules.</summary>
public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(c => c.RegistrationNumber).NotEmpty().MaximumLength(16)
            .Matches("^[A-Z0-9 -]+$").WithMessage("Registration must be uppercase letters/digits.");
        RuleFor(c => c.Capacity).InclusiveBetween(1, 100);
    }
}

/// <summary>Creates the vehicle after a registration uniqueness check.</summary>
public sealed class CreateVehicleCommandHandler : IRequestHandler<CreateVehicleCommand, VehicleDto>
{
    private readonly IApplicationDbContext _db;

    public CreateVehicleCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<VehicleDto> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        var registration = request.RegistrationNumber.Trim().ToUpperInvariant();
        if (await _db.Vehicles.AnyAsync(v => v.RegistrationNumber == registration, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"Vehicle '{registration}' is already registered.");
        }

        var vehicle = new Vehicle
        {
            RegistrationNumber = registration,
            Model = request.Model?.Trim(),
            Capacity = request.Capacity,
            InsuranceExpiry = request.InsuranceExpiry,
            FitnessExpiry = request.FitnessExpiry,
        };
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VehicleDto(vehicle.Id, vehicle.RegistrationNumber, vehicle.Model,
            vehicle.Capacity, vehicle.InsuranceExpiry, vehicle.FitnessExpiry, vehicle.Status);
    }
}

/// <summary>Lists vehicles.</summary>
public sealed record GetVehiclesQuery : IRequest<IReadOnlyList<VehicleDto>>;

/// <summary>Simple projection query.</summary>
public sealed class GetVehiclesQueryHandler : IRequestHandler<GetVehiclesQuery, IReadOnlyList<VehicleDto>>
{
    private readonly IApplicationDbContext _db;

    public GetVehiclesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<VehicleDto>> Handle(
        GetVehiclesQuery request, CancellationToken cancellationToken) =>
        await _db.Vehicles.AsNoTracking()
            .OrderBy(v => v.RegistrationNumber)
            .Select(v => new VehicleDto(v.Id, v.RegistrationNumber, v.Model,
                v.Capacity, v.InsuranceExpiry, v.FitnessExpiry, v.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>Creates a route with its ordered stops.</summary>
public sealed record CreateRouteCommand(
    string Name,
    Guid? VehicleId,
    string? DriverName,
    string? DriverPhone,
    IReadOnlyList<RouteStopInput> Stops) : IRequest<Guid>;

/// <summary>Route shape rules.</summary>
public sealed class CreateRouteCommandValidator : AbstractValidator<CreateRouteCommand>
{
    public CreateRouteCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.DriverPhone).Matches(@"^\+?[0-9]{10,15}$")
            .When(c => !string.IsNullOrWhiteSpace(c.DriverPhone));
        RuleFor(c => c.Stops).NotEmpty()
            .Must(s => s.Select(x => x.Name.Trim().ToUpperInvariant()).Distinct().Count() == s.Count)
            .WithMessage("Stop names must be unique.");
        RuleForEach(c => c.Stops).ChildRules(stop =>
        {
            stop.RuleFor(s => s.Name).NotEmpty().MaximumLength(128);
        });
    }
}

/// <summary>Creates the route + stops after reference checks.</summary>
public sealed class CreateRouteCommandHandler : IRequestHandler<CreateRouteCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateRouteCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.TransportRoutes.AnyAsync(r => r.Name == name, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"Route '{name}' already exists.");
        }

        if (request.VehicleId is { } vehicleId &&
            !await _db.Vehicles.AnyAsync(v => v.Id == vehicleId, cancellationToken).ConfigureAwait(false))
        {
            throw new NotFoundException("Vehicle", vehicleId);
        }

        var route = new TransportRoute
        {
            Name = name,
            VehicleId = request.VehicleId,
            DriverName = request.DriverName?.Trim(),
            DriverPhone = request.DriverPhone?.Trim(),
            Stops = request.Stops
                .Select((stop, index) => new RouteStop
                {
                    Name = stop.Name.Trim(),
                    SortOrder = index + 1,
                    PickupTime = stop.PickupTime,
                    Latitude = stop.Latitude,
                    Longitude = stop.Longitude,
                })
                .ToList(),
        };
        _db.TransportRoutes.Add(route);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return route.Id;
    }
}

/// <summary>Lists routes with stops and rider counts.</summary>
public sealed record GetRoutesQuery : IRequest<IReadOnlyList<TransportRouteDto>>;

/// <summary>Projection including stops ordered by position.</summary>
public sealed class GetRoutesQueryHandler : IRequestHandler<GetRoutesQuery, IReadOnlyList<TransportRouteDto>>
{
    private readonly IApplicationDbContext _db;

    public GetRoutesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TransportRouteDto>> Handle(
        GetRoutesQuery request, CancellationToken cancellationToken) =>
        await _db.TransportRoutes.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new TransportRouteDto(
                r.Id,
                r.Name,
                r.VehicleId,
                r.Vehicle != null ? r.Vehicle.RegistrationNumber : null,
                r.DriverName,
                r.DriverPhone,
                _db.StudentTransportAssignments.Count(a => a.RouteId == r.Id),
                r.Stops.OrderBy(s => s.SortOrder)
                    .Select(s => new RouteStopDto(
                        s.Id, s.Name, s.SortOrder, s.PickupTime, s.Latitude, s.Longitude))
                    .ToList()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>Allocates (or moves) a student to a route stop.</summary>
public sealed record AssignStudentTransportCommand(
    Guid StudentId, Guid RouteId, Guid StopId) : IRequest;

/// <summary>Upserts the single allocation per student.</summary>
public sealed class AssignStudentTransportCommandHandler
    : IRequestHandler<AssignStudentTransportCommand>
{
    private readonly IApplicationDbContext _db;

    public AssignStudentTransportCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(AssignStudentTransportCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(Student), request.StudentId);
        }

        var stopBelongsToRoute = await _db.RouteStops
            .AnyAsync(s => s.Id == request.StopId && s.RouteId == request.RouteId, cancellationToken)
            .ConfigureAwait(false);
        if (!stopBelongsToRoute)
        {
            throw new NotFoundException("Stop (on this route)", request.StopId);
        }

        var existing = await _db.StudentTransportAssignments
            .FirstOrDefaultAsync(a => a.StudentId == request.StudentId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
        {
            _db.StudentTransportAssignments.Add(new StudentTransportAssignment
            {
                StudentId = request.StudentId,
                RouteId = request.RouteId,
                StopId = request.StopId,
            });
        }
        else
        {
            existing.RouteId = request.RouteId;
            existing.StopId = request.StopId;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>A child's transport info (parent app). Null result = no allocation.</summary>
public sealed record GetChildTransportQuery(Guid StudentId) : IRequest<ChildTransportDto?>;

/// <summary>Composes route/stop/driver/vehicle for one student.</summary>
public sealed class GetChildTransportQueryHandler
    : IRequestHandler<GetChildTransportQuery, ChildTransportDto?>
{
    private readonly IApplicationDbContext _db;

    public GetChildTransportQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ChildTransportDto?> Handle(
        GetChildTransportQuery request, CancellationToken cancellationToken) =>
        await _db.StudentTransportAssignments.AsNoTracking()
            .Where(a => a.StudentId == request.StudentId)
            .Select(a => new ChildTransportDto(
                a.Route!.Name,
                a.Stop!.Name,
                a.Stop.PickupTime,
                a.Route.DriverName,
                a.Route.DriverPhone,
                a.Route.Vehicle != null ? a.Route.Vehicle.RegistrationNumber : null))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
