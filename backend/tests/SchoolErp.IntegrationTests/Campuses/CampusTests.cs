using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Campuses;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Campuses;

/// <summary>
/// A container plus a way to mint a fresh school per test. "The first campus
/// becomes primary" only means something in a school that has none, and xUnit
/// gives no ordering guarantee inside a class, so tests must not share one.
/// </summary>
public sealed class CampusFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_campus_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;
    private int _schoolCounter;

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
        services.AddScoped<GuidCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<GuidCurrentUser>());
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Migrating against an empty catalog means the backfill in
        // AddCampusesAndInstitutionType has no tenants to touch, so every
        // school minted below starts with a genuinely empty campus list.
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>A brand-new school with no campuses, private to one test.</summary>
    public async Task<Guid> NewSchoolAsync(InstitutionType type = InstitutionType.School)
    {
        var n = Interlocked.Increment(ref _schoolCounter);
        var id = Guid.NewGuid();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = id,
            Code = $"CAMP{n:D2}",
            Name = $"Campus Test Institution {n}",
            Subdomain = $"camptest{n}",
            InstitutionType = type,
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
        return id;
    }

    public AsyncServiceScope CreateScope(Guid tenantId)
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        return scope;
    }
}

/// <summary>The campus register: one head location, several branches.</summary>
public sealed class CampusTests : IClassFixture<CampusFixture>
{
    private readonly CampusFixture _fixture;

    public CampusTests(CampusFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task The_first_campus_is_primary_and_later_ones_are_not()
    {
        await using var scope = _fixture.CreateScope(
            await _fixture.NewSchoolAsync(InstitutionType.College));
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var headId = await sender.Send(new CreateCampusCommand(
            "City Campus", "city", "1 College Road", "Vijayawada", "Andhra Pradesh",
            "520001", "+918661234567"));
        var branchId = await sender.Send(new CreateCampusCommand(
            "Science Block", "SCI", null, null, null, null, null));

        var campuses = await sender.Send(new GetCampusesQuery());

        // Primary first, and the code is stored the way staff will type it.
        campuses.Select(c => c.Id).Should().Equal(headId, branchId);
        campuses[0].IsPrimary.Should().BeTrue();
        campuses[0].Code.Should().Be("CITY");
        campuses[0].City.Should().Be("Vijayawada");
        campuses[1].IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task A_campus_code_cannot_repeat_within_a_school()
    {
        await using var scope = _fixture.CreateScope(await _fixture.NewSchoolAsync());
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateCampusCommand(
            "Main Campus", "MAIN", null, "Hyderabad", "Telangana", null, null));

        var duplicate = () => sender.Send(new CreateCampusCommand(
            "Main Campus Annexe", "main", null, null, null, null, null));
        await duplicate.Should().ThrowAsync<ConflictException>()
            .WithMessage("*MAIN*already exists*");
    }

    [Fact]
    public async Task The_same_code_is_free_in_another_school()
    {
        var first = await _fixture.NewSchoolAsync();
        var second = await _fixture.NewSchoolAsync();

        await using (var scope = _fixture.CreateScope(first))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateCampusCommand(
                "Shared Code Campus", "SHARED", null, null, null, null, null));
        }

        await using (var scope = _fixture.CreateScope(second))
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var id = await sender.Send(new CreateCampusCommand(
                "Also Shared", "SHARED", null, null, null, null, null));

            var campuses = await sender.Send(new GetCampusesQuery());
            campuses.Should().Contain(c => c.Id == id);
            campuses.Should().NotContain(c => c.Name == "Shared Code Campus");
        }
    }

    [Fact]
    public async Task The_primary_campus_cannot_be_closed_but_a_branch_can()
    {
        await using var scope = _fixture.CreateScope(await _fixture.NewSchoolAsync());
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var headId = await sender.Send(new CreateCampusCommand(
            "Closure Head", "CLHEAD", null, null, null, null, null));
        var branchId = await sender.Send(new CreateCampusCommand(
            "Closure Branch", "CLBRCH", null, null, null, null, null));

        var closeHead = () => sender.Send(new UpdateCampusCommand(
            headId, "Closure Head", "CLHEAD", null, null, null, null, null, IsActive: false));
        await closeHead.Should().ThrowAsync<ConflictException>()
            .WithMessage("*primary campus*");

        await sender.Send(new UpdateCampusCommand(
            branchId, "Closure Branch", "CLBRCH", null, null, null, null, null, IsActive: false));

        // Closed campuses drop out of the default list but are still reachable.
        (await sender.Send(new GetCampusesQuery()))
            .Should().NotContain(c => c.Id == branchId);
        (await sender.Send(new GetCampusesQuery(IncludeInactive: true)))
            .Should().Contain(c => c.Id == branchId && !c.IsActive);

        // ...and a closed campus cannot be promoted to head.
        var promoteClosed = () => sender.Send(new SetPrimaryCampusCommand(branchId));
        await promoteClosed.Should().ThrowAsync<ConflictException>()
            .WithMessage("*closed campus*");
    }

    [Fact]
    public async Task Making_a_branch_primary_steps_the_old_head_down()
    {
        await using var scope = _fixture.CreateScope(await _fixture.NewSchoolAsync());
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var oldHeadId = await sender.Send(new CreateCampusCommand(
            "Move Head", "MVHEAD", null, null, null, null, null));
        var branchId = await sender.Send(new CreateCampusCommand(
            "Move Branch", "MVBRCH", null, null, null, null, null));

        await sender.Send(new SetPrimaryCampusCommand(branchId));

        var campuses = await sender.Send(new GetCampusesQuery(IncludeInactive: true));
        campuses.Single(c => c.Id == branchId).IsPrimary.Should().BeTrue();
        campuses.Single(c => c.Id == oldHeadId).IsPrimary.Should().BeFalse();

        // Exactly one head at all times.
        campuses.Count(c => c.IsPrimary).Should().Be(1);
    }
}
