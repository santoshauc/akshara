using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Application.Transport;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Domain.Transport;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Auth;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Transport;

/// <summary>
/// One school, one route with a driver phone, one rider — the full trip
/// lifecycle: inspection gate, GPS, board/drop SMS, live tracking.
/// </summary>
public sealed class TripLifecycleFixture : IAsyncLifetime
{
    public const string DriverPhone = "+919888800001";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_trip_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid RouteId { get; private set; }

    public Guid StudentId { get; private set; }

    public RecordingSmsSender SmsSender { get; } = new();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        services.AddSingleton<ISmsSender>(SmsSender);
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TRIP01",
                Name = "Trip Test School",
                Subdomain = "triptest",
                Status = TenantStatus.Active,
                SmsCredits = 1_000,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateAcademicYearCommand(
                "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
            var grade8 = await sender.Send(new CreateClassCommand("Grade 8", 8, ["A"]));

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Ishaan", "Gupta", new DateOnly(2013, 9, 9), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, grade8.Id, grade8.Sections.Single().Id, 1,
                [new GuardianInput("Maya", "Gupta", GuardianRelation.Mother, "+919888800099", null, null, true)]));

            RouteId = await sender.Send(new CreateRouteCommand(
                "Trip Route", null, "Suresh", DriverPhone,
                [new RouteStopInput("Stop A", new TimeOnly(7, 0), null, null)]));

            var routes = await sender.Send(new GetRoutesQuery());
            var stopId = routes.Single(r => r.Id == RouteId).Stops.Single().Id;
            await sender.Send(new AssignStudentTransportCommand(StudentId, RouteId, stopId));
        }
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope CreateScope()
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(TenantId);
        return scope;
    }
}

/// <summary>The trip lifecycle through the full pipeline.</summary>
public sealed class TripLifecycleTests : IClassFixture<TripLifecycleFixture>
{
    private readonly TripLifecycleFixture _fixture;

    public TripLifecycleTests(TripLifecycleFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Trip_cannot_start_without_the_inspection_checklist()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new StartTripCommand(
            null, TripLifecycleFixture.DriverPhone, TripType.Pickup,
            InspectionOk: false, InspectionNotes: null));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*inspection*");
    }

    [Fact]
    public async Task Full_lifecycle_start_ping_board_track_drop_end()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Start (inspection done) — manifest shows our rider and the active trip.
        await sender.Send(new StartTripCommand(
            null, TripLifecycleFixture.DriverPhone, TripType.Pickup, true, "All good"));
        var route = await sender.Send(new GetDriverRouteQuery(null, TripLifecycleFixture.DriverPhone));
        route.ActiveTripId.Should().NotBeNull();
        route.Riders.Should().ContainSingle(r => r.StudentName == "Ishaan Gupta");

        // Second start on the same route must conflict.
        var doubleStart = () => sender.Send(new StartTripCommand(
            null, TripLifecycleFixture.DriverPhone, TripType.Pickup, true, null));
        await doubleStart.Should().ThrowAsync<ConflictException>().WithMessage("*in progress*");

        // GPS ping → parent's live-tracking query sees it.
        await sender.Send(new RecordLocationCommand(
            null, TripLifecycleFixture.DriverPhone, 17.4326m, 78.4071m));
        var bus = await sender.Send(new GetBusLocationQuery(_fixture.StudentId));
        bus.Should().NotBeNull();
        bus!.Latitude.Should().Be(17.4326m);
        bus.TripType.Should().Be(TripType.Pickup);

        // Board → guardian SMS queued exactly once (idempotent on repeat).
        await sender.Send(new MarkRiderEventCommand(
            null, TripLifecycleFixture.DriverPhone, _fixture.StudentId,
            TripStudentEventType.PickedUp, null));
        await sender.Send(new MarkRiderEventCommand(
            null, TripLifecycleFixture.DriverPhone, _fixture.StudentId,
            TripStudentEventType.PickedUp, null));

        var outbox = await db.OutboxMessages
            .Where(m => m.TenantId == _fixture.TenantId)
            .ToListAsync();
        outbox.Count(m => m.Payload.Contains("boarded")).Should().Be(1);
        outbox.Single(m => m.Payload.Contains("boarded")).Payload
            .Should().Contain("Ishaan").And.Contain("919888800099");

        // End → live tracking goes quiet.
        await sender.Send(new EndTripCommand(null, TripLifecycleFixture.DriverPhone));
        (await sender.Send(new GetBusLocationQuery(_fixture.StudentId))).Should().BeNull();
        (await sender.Send(new GetDriverRouteQuery(null, TripLifecycleFixture.DriverPhone)))
            .ActiveTripId.Should().BeNull();
    }

    [Fact]
    public async Task A_stranger_phone_has_no_route()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new GetDriverRouteQuery(null, "+911111199999"));
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
