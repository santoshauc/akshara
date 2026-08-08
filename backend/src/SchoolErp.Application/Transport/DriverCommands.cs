using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Transport;

namespace SchoolErp.Application.Transport;

/// <summary>One rider on the driver's manifest.</summary>
public sealed record ManifestRiderDto(
    Guid StudentId, string StudentName, string? ClassName, string StopName, int StopOrder);

/// <summary>The driver's route with stops, vehicle and riders.</summary>
public sealed record DriverRouteDto(
    Guid RouteId,
    string RouteName,
    string? VehicleRegistration,
    IReadOnlyList<RouteStopDto> Stops,
    IReadOnlyList<ManifestRiderDto> Riders,
    Guid? ActiveTripId,
    TripType? ActiveTripType);

/// <summary>
/// Resolves the route a signed-in driver operates (matched by linked user id
/// or verified phone). Every driver-app call goes through this — a driver can
/// only ever see and act on their own route.
/// </summary>
public sealed class DriverAccess
{
    private readonly IApplicationDbContext _db;

    public DriverAccess(IApplicationDbContext db) => _db = db;

    /// <summary>The driver's route id, or throws NotFound when none matches.</summary>
    public async Task<Guid> GetMyRouteIdAsync(string? userId, string? userPhone, CancellationToken ct)
    {
        _ = Guid.TryParse(userId, out var userGuid);

        var routeId = await _db.TransportRoutes
            .Where(r => (userGuid != Guid.Empty && r.DriverUserId == userGuid) ||
                        (userPhone != null && r.DriverPhone == userPhone))
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return routeId ?? throw new NotFoundException("Route (for this driver)", userId ?? "?");
    }
}

/// <summary>The driver's route, manifest and any active trip.</summary>
public sealed record GetDriverRouteQuery(string? UserId, string? UserPhone) : IRequest<DriverRouteDto>;

/// <summary>Composes the manifest grouped by stop order.</summary>
public sealed class GetDriverRouteQueryHandler : IRequestHandler<GetDriverRouteQuery, DriverRouteDto>
{
    private readonly IApplicationDbContext _db;
    private readonly DriverAccess _access;

    public GetDriverRouteQueryHandler(IApplicationDbContext db, DriverAccess access)
    {
        _db = db;
        _access = access;
    }

    public async Task<DriverRouteDto> Handle(GetDriverRouteQuery request, CancellationToken cancellationToken)
    {
        var routeId = await _access.GetMyRouteIdAsync(request.UserId, request.UserPhone, cancellationToken)
            .ConfigureAwait(false);

        var route = await _db.TransportRoutes.AsNoTracking()
            .Include(r => r.Vehicle)
            .Include(r => r.Stops)
            .FirstAsync(r => r.Id == routeId, cancellationToken)
            .ConfigureAwait(false);

        var riders = await _db.StudentTransportAssignments.AsNoTracking()
            .Where(a => a.RouteId == routeId)
            .Select(a => new ManifestRiderDto(
                a.StudentId,
                _db.Students.Where(s => s.Id == a.StudentId)
                    .Select(s => (s.FirstName + " " + s.LastName).Trim()).First(),
                _db.Enrollments
                    .Where(e => e.StudentId == a.StudentId && e.AcademicYear!.IsCurrent)
                    .Select(e => e.SchoolClass!.Name).FirstOrDefault(),
                a.Stop!.Name,
                a.Stop.SortOrder))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeTrip = await _db.Trips.AsNoTracking()
            .Where(t => t.RouteId == routeId && t.Status == TripStatus.InProgress)
            .Select(t => new { t.Id, t.Type })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DriverRouteDto(
            route.Id,
            route.Name,
            route.Vehicle?.RegistrationNumber,
            route.Stops.OrderBy(s => s.SortOrder)
                .Select(s => new RouteStopDto(s.Id, s.Name, s.SortOrder, s.PickupTime, s.Latitude, s.Longitude))
                .ToList(),
            riders.OrderBy(r => r.StopOrder).ThenBy(r => r.StudentName).ToList(),
            activeTrip?.Id,
            activeTrip?.Type);
    }
}

/// <summary>
/// Starts a trip. The pre-trip inspection checklist is a hard gate: the
/// command is rejected unless the driver confirmed it.
/// </summary>
public sealed record StartTripCommand(
    string? UserId,
    string? UserPhone,
    TripType Type,
    bool InspectionOk,
    string? InspectionNotes) : IRequest<Guid>;

/// <summary>Trip-start rules.</summary>
public sealed class StartTripCommandValidator : AbstractValidator<StartTripCommand>
{
    public StartTripCommandValidator()
    {
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.InspectionNotes).MaximumLength(512);
    }
}

/// <summary>Creates the trip after the inspection and single-active checks.</summary>
public sealed class StartTripCommandHandler : IRequestHandler<StartTripCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly DriverAccess _access;
    private readonly TimeProvider _clock;

    public StartTripCommandHandler(IApplicationDbContext db, DriverAccess access, TimeProvider clock)
    {
        _db = db;
        _access = access;
        _clock = clock;
    }

    public async Task<Guid> Handle(StartTripCommand request, CancellationToken cancellationToken)
    {
        if (!request.InspectionOk)
        {
            throw new ConflictException(
                "Complete the vehicle inspection checklist before starting the trip.");
        }

        var routeId = await _access.GetMyRouteIdAsync(request.UserId, request.UserPhone, cancellationToken)
            .ConfigureAwait(false);

        var hasActive = await _db.Trips
            .AnyAsync(t => t.RouteId == routeId && t.Status == TripStatus.InProgress, cancellationToken)
            .ConfigureAwait(false);
        if (hasActive)
        {
            throw new ConflictException("A trip is already in progress on this route. End it first.");
        }

        var trip = new Trip
        {
            RouteId = routeId,
            Type = request.Type,
            StartedAt = _clock.GetUtcNow(),
            InspectionOk = true,
            InspectionNotes = request.InspectionNotes?.Trim(),
        };
        _db.Trips.Add(trip);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return trip.Id;
    }
}

/// <summary>Appends a GPS ping to the driver's active trip.</summary>
public sealed record RecordLocationCommand(
    string? UserId, string? UserPhone, decimal Latitude, decimal Longitude) : IRequest;

/// <summary>Ping bounds.</summary>
public sealed class RecordLocationCommandValidator : AbstractValidator<RecordLocationCommand>
{
    public RecordLocationCommandValidator()
    {
        RuleFor(c => c.Latitude).InclusiveBetween(-90, 90);
        RuleFor(c => c.Longitude).InclusiveBetween(-180, 180);
    }
}

/// <summary>Stores the ping against the active trip.</summary>
public sealed class RecordLocationCommandHandler : IRequestHandler<RecordLocationCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly DriverAccess _access;
    private readonly TimeProvider _clock;

    public RecordLocationCommandHandler(IApplicationDbContext db, DriverAccess access, TimeProvider clock)
    {
        _db = db;
        _access = access;
        _clock = clock;
    }

    public async Task Handle(RecordLocationCommand request, CancellationToken cancellationToken)
    {
        var routeId = await _access.GetMyRouteIdAsync(request.UserId, request.UserPhone, cancellationToken)
            .ConfigureAwait(false);

        var tripId = await _db.Trips
            .Where(t => t.RouteId == routeId && t.Status == TripStatus.InProgress)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Active trip", routeId);

        _db.TripLocations.Add(new TripLocation
        {
            TripId = tripId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RecordedAt = _clock.GetUtcNow(),
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Marks a rider event (boarded/dropped/absent). Boarded and dropped queue an
/// SMS to the primary guardian via the outbox — the spec's pickup/drop
/// notifications.
/// </summary>
public sealed record MarkRiderEventCommand(
    string? UserId,
    string? UserPhone,
    Guid StudentId,
    TripStudentEventType EventType,
    string? Remarks) : IRequest;

/// <summary>Records the event and queues the guardian notification.</summary>
public sealed class MarkRiderEventCommandHandler : IRequestHandler<MarkRiderEventCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly DriverAccess _access;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLookup _tenantLookup;
    private readonly TimeProvider _clock;

    public MarkRiderEventCommandHandler(
        IApplicationDbContext db,
        DriverAccess access,
        ITenantContext tenantContext,
        ITenantLookup tenantLookup,
        TimeProvider clock)
    {
        _db = db;
        _access = access;
        _tenantContext = tenantContext;
        _tenantLookup = tenantLookup;
        _clock = clock;
    }

    public async Task Handle(MarkRiderEventCommand request, CancellationToken cancellationToken)
    {
        var routeId = await _access.GetMyRouteIdAsync(request.UserId, request.UserPhone, cancellationToken)
            .ConfigureAwait(false);

        var tripId = await _db.Trips
            .Where(t => t.RouteId == routeId && t.Status == TripStatus.InProgress)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Active trip", routeId);

        var isRider = await _db.StudentTransportAssignments
            .AnyAsync(a => a.RouteId == routeId && a.StudentId == request.StudentId, cancellationToken)
            .ConfigureAwait(false);
        if (!isRider)
        {
            throw new NotFoundException("Rider (on this route)", request.StudentId);
        }

        var duplicate = await _db.TripStudentEvents.AnyAsync(
                e => e.TripId == tripId && e.StudentId == request.StudentId &&
                     e.EventType == request.EventType,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            return; // idempotent — no duplicate SMS
        }

        _db.TripStudentEvents.Add(new TripStudentEvent
        {
            TripId = tripId,
            StudentId = request.StudentId,
            EventType = request.EventType,
            RecordedAt = _clock.GetUtcNow(),
            Remarks = request.Remarks?.Trim(),
        });

        if (request.EventType is TripStudentEventType.PickedUp or TripStudentEventType.Dropped)
        {
            await QueueGuardianSmsAsync(request.StudentId, request.EventType, cancellationToken)
                .ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task QueueGuardianSmsAsync(
        Guid studentId, TripStudentEventType eventType, CancellationToken ct)
    {
        var contact = await _db.StudentGuardians
            .Where(sg => sg.StudentId == studentId && sg.IsPrimary && sg.Guardian != null)
            .Select(sg => new
            {
                sg.Guardian!.Phone,
                StudentName = _db.Students.Where(s => s.Id == studentId)
                    .Select(s => s.FirstName).First(),
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (contact is null)
        {
            return;
        }

        var tenant = await _tenantLookup.FindByIdAsync(_tenantContext.TenantId, ct).ConfigureAwait(false);
        var action = eventType == TripStudentEventType.PickedUp
            ? "boarded the school bus"
            : "was dropped off by the school bus";

        await Notifications.NotificationQueue.QueueGuardianAsync(
            _db, _tenantContext.TenantId, contact.Phone,
            eventType == TripStudentEventType.PickedUp ? "On the bus" : "Dropped off",
            $"{contact.StudentName} {action} just now. — {tenant?.Name ?? "School"}",
            ct).ConfigureAwait(false);
    }
}

/// <summary>Ends the driver's active trip.</summary>
public sealed record EndTripCommand(string? UserId, string? UserPhone) : IRequest;

/// <summary>Completes the trip.</summary>
public sealed class EndTripCommandHandler : IRequestHandler<EndTripCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly DriverAccess _access;
    private readonly TimeProvider _clock;

    public EndTripCommandHandler(IApplicationDbContext db, DriverAccess access, TimeProvider clock)
    {
        _db = db;
        _access = access;
        _clock = clock;
    }

    public async Task Handle(EndTripCommand request, CancellationToken cancellationToken)
    {
        var routeId = await _access.GetMyRouteIdAsync(request.UserId, request.UserPhone, cancellationToken)
            .ConfigureAwait(false);

        var trip = await _db.Trips
            .FirstOrDefaultAsync(
                t => t.RouteId == routeId && t.Status == TripStatus.InProgress, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Active trip", routeId);

        trip.Status = TripStatus.Completed;
        trip.EndedAt = _clock.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Live bus state for one child (parent app).</summary>
public sealed record BusLocationDto(
    TripType TripType,
    DateTimeOffset StartedAt,
    decimal? Latitude,
    decimal? Longitude,
    DateTimeOffset? LastSeenAt);

/// <summary>Latest ping of the active trip on the child's route; null when idle.</summary>
public sealed record GetBusLocationQuery(Guid StudentId) : IRequest<BusLocationDto?>;

/// <summary>Composes the live-tracking answer.</summary>
public sealed class GetBusLocationQueryHandler : IRequestHandler<GetBusLocationQuery, BusLocationDto?>
{
    private readonly IApplicationDbContext _db;

    public GetBusLocationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BusLocationDto?> Handle(
        GetBusLocationQuery request, CancellationToken cancellationToken)
    {
        var routeId = await _db.StudentTransportAssignments.AsNoTracking()
            .Where(a => a.StudentId == request.StudentId)
            .Select(a => (Guid?)a.RouteId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (routeId is null)
        {
            return null;
        }

        var trip = await _db.Trips.AsNoTracking()
            .Where(t => t.RouteId == routeId && t.Status == TripStatus.InProgress)
            .Select(t => new { t.Id, t.Type, t.StartedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (trip is null)
        {
            return null;
        }

        var ping = await _db.TripLocations.AsNoTracking()
            .Where(l => l.TripId == trip.Id)
            .OrderByDescending(l => l.RecordedAt)
            .Select(l => new { l.Latitude, l.Longitude, l.RecordedAt })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new BusLocationDto(
            trip.Type, trip.StartedAt, ping?.Latitude, ping?.Longitude, ping?.RecordedAt);
    }
}
