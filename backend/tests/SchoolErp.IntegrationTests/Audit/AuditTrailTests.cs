using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Audit;
using SchoolErp.Domain.Audit;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Audit;

/// <summary>One school; the audit pipeline behavior is under test.</summary>
public sealed class AuditTrailFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_audit_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

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

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Code = "AUDIT1",
            Name = "Audit Test School",
            Subdomain = "audittest",
            Status = TenantStatus.Active,
            SmsCredits = 1_000,
        });
        await db.SaveChangesAsync();
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

/// <summary>Every successful command leaves a trail; queries never do.</summary>
public sealed class AuditTrailTests : IClassFixture<AuditTrailFixture>
{
    private readonly AuditTrailFixture _fixture;

    public AuditTrailTests(AuditTrailFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Successful_commands_are_audited_with_user_and_tenant()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateClassCommand("Grade 1", 1, ["A"]));

        var trail = await sender.Send(new GetAuditTrailQuery());
        var entry = trail.Should().Contain(e => e.Action == "CreateClassCommand").Subject;
        entry.UserId.Should().Be("test-user");
        entry.UserName.Should().Be("Integration Test");

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.AuditEvents.IgnoreQueryFilters()
                .SingleAsync(e => e.Id == entry.Id))
            .TenantId.Should().Be(_fixture.TenantId);
    }

    [Fact]
    public async Task Queries_are_never_audited()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var before = await db.AuditEvents.CountAsync();
        await sender.Send(new GetAcademicYearsQuery());
        await sender.Send(new GetAuditTrailQuery());
        var after = await db.AuditEvents.CountAsync();

        after.Should().Be(before, "reads must not pollute the action trail");
    }

    [Fact]
    public async Task The_trail_is_scoped_to_the_callers_school()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // A row belonging to some OTHER school, inserted directly.
        var foreign = new AuditEvent
        {
            TenantId = Guid.NewGuid(),
            Action = "ForeignSchoolCommand",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        db.AuditEvents.Add(foreign);
        await db.SaveChangesAsync();

        var trail = await sender.Send(new GetAuditTrailQuery());
        trail.Should().NotContain(e => e.Action == "ForeignSchoolCommand",
            "a school admin must never see another school's audit trail");

        var search = await sender.Send(new GetAuditTrailQuery(Search: "ForeignSchool"));
        search.Should().BeEmpty();
    }
}
