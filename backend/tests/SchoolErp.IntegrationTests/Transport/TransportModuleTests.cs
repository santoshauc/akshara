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
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Transport;

/// <summary>One school with a student ready to be allocated to a route.</summary>
public sealed class TransportModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_transport_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid StudentId { get; private set; }

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
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TRNS01",
                Name = "Transport Test School",
                Subdomain = "transtest",
                Status = TenantStatus.Active,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateAcademicYearCommand(
                "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
            var grade6 = await sender.Send(new CreateClassCommand("Grade 6", 6, ["A"]));

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Rohan", "Verma", new DateOnly(2015, 4, 1), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, grade6.Id, grade6.Sections.Single().Id, 1,
                [new GuardianInput("Parent", "Verma", GuardianRelation.Father, "+919200000001", null, null, true)]));
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

/// <summary>Transport behavior through the full pipeline.</summary>
public sealed class TransportModuleTests : IClassFixture<TransportModuleFixture>
{
    private readonly TransportModuleFixture _fixture;

    public TransportModuleTests(TransportModuleFixture fixture) => _fixture = fixture;

    private async Task<(Guid RouteId, Guid Stop1, Guid Stop2)> CreateRouteAsync(
        ISender sender, string name, Guid? vehicleId = null)
    {
        var routeId = await sender.Send(new CreateRouteCommand(
            name, vehicleId, "Ramesh Kumar", "+919888877766",
            [
                new RouteStopInput("Jubilee Hills", new TimeOnly(7, 15), 17.4326m, 78.4071m),
                new RouteStopInput("Banjara Hills", new TimeOnly(7, 30), 17.4108m, 78.4294m),
            ]));

        var routes = await sender.Send(new GetRoutesQuery());
        var route = routes.Single(r => r.Id == routeId);
        return (routeId, route.Stops[0].Id, route.Stops[1].Id);
    }

    [Fact]
    public async Task Route_with_vehicle_and_ordered_stops_roundtrips()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var vehicle = await sender.Send(new CreateVehicleCommand(
            "TS09AB1234", "Tata Starbus", 40, new DateOnly(2027, 3, 31), new DateOnly(2027, 6, 30)));
        var (routeId, _, _) = await CreateRouteAsync(sender, "Route 1 — West", vehicle.Id);

        var routes = await sender.Send(new GetRoutesQuery());
        var route = routes.Single(r => r.Id == routeId);
        route.VehicleRegistration.Should().Be("TS09AB1234");
        route.DriverPhone.Should().Be("+919888877766");
        route.Stops.Should().HaveCount(2);
        route.Stops.Select(s => s.SortOrder).Should().ContainInOrder(1, 2);
        route.Stops[0].Name.Should().Be("Jubilee Hills");
    }

    [Fact]
    public async Task Assignment_upserts_and_child_transport_reflects_the_latest_stop()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (routeId, stop1, stop2) = await CreateRouteAsync(sender, "Route 2 — East");

        await sender.Send(new AssignStudentTransportCommand(_fixture.StudentId, routeId, stop1));
        // Family moved house — reassign to the other stop.
        await sender.Send(new AssignStudentTransportCommand(_fixture.StudentId, routeId, stop2));

        var transport = await sender.Send(new GetChildTransportQuery(_fixture.StudentId));
        transport.Should().NotBeNull();
        transport!.StopName.Should().Be("Banjara Hills");
        transport.PickupTime.Should().Be(new TimeOnly(7, 30));
        transport.DriverName.Should().Be("Ramesh Kumar");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.StudentTransportAssignments.CountAsync(a => a.StudentId == _fixture.StudentId))
            .Should().Be(1, "reassignment must upsert, not duplicate");
    }

    [Fact]
    public async Task Assigning_to_a_stop_of_a_different_route_is_rejected()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (routeA, _, _) = await CreateRouteAsync(sender, "Route 3 — North");
        var (_, stopOfB, _) = await CreateRouteAsync(sender, "Route 4 — South");

        var act = () => sender.Send(new AssignStudentTransportCommand(
            _fixture.StudentId, routeA, stopOfB));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Duplicate_vehicle_registration_conflicts()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateVehicleCommand("TS10CD5678", null, 30, null, null));
        var act = () => sender.Send(new CreateVehicleCommand("TS10CD5678", null, 30, null, null));
        await act.Should().ThrowAsync<ConflictException>();
    }
}
