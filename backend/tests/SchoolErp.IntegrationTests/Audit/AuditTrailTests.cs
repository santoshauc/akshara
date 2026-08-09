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

    /// <summary>Scope with NO tenant bound — how a platform operator arrives.</summary>
    public AsyncServiceScope CreatePlatformScope() => _provider.CreateAsyncScope();
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

    [Fact]
    public async Task A_platform_operator_sees_operator_actions_not_every_school_at_once()
    {
        await using (var seed = _fixture.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.AddRange(
                new AuditEvent
                {
                    TenantId = null, // an operator acting on the platform
                    UserId = "test-user", // a real operator action always has one
                    UserName = "Platform Super Admin",
                    Action = "RecordSmsTopUpCommand",
                    OccurredAt = DateTimeOffset.UtcNow,
                },
                new AuditEvent
                {
                    TenantId = _fixture.TenantId,
                    Action = "SchoolSideCommand",
                    OccurredAt = DateTimeOffset.UtcNow,
                },
                new AuditEvent
                {
                    // Anonymous public traffic: no tenant, but nobody did it.
                    TenantId = null,
                    UserId = null,
                    Action = "SubmitPublicEnquiryCommand",
                    OccurredAt = DateTimeOffset.UtcNow,
                });
            await db.SaveChangesAsync();
        }

        await using var scope = _fixture.CreatePlatformScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var operatorTrail = await sender.Send(new GetAuditTrailQuery());
        operatorTrail.Should().Contain(e => e.Action == "RecordSmsTopUpCommand",
            "operator actions are exactly what this view exists for");
        operatorTrail.Should().NotContain(e => e.Action == "SchoolSideCommand",
            "every school's rows at once drowns the operator trail");
        operatorTrail.Should().NotContain(e => e.Action == "SubmitPublicEnquiryCommand",
            "anonymous public traffic has no tenant either, but no operator did it");

        // Support still reaches one named school, deliberately.
        var schoolTrail = await sender.Send(
            new GetAuditTrailQuery(SchoolId: _fixture.TenantId));
        schoolTrail.Should().Contain(e => e.Action == "SchoolSideCommand");
        schoolTrail.Should().OnlyContain(e => e.SchoolName != null,
            "a platform view labels which school a row came from");
    }

    [Fact]
    public async Task A_school_admin_cannot_read_another_school_by_asking_for_it()
    {
        var other = Guid.NewGuid();
        await using (var seed = _fixture.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.Add(new AuditEvent
            {
                TenantId = other,
                Action = "SomeoneElsesCommand",
                OccurredAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // SchoolId is ignored for a tenant-bound caller — the tenant decides.
        var trail = await sender.Send(new GetAuditTrailQuery(SchoolId: other));
        trail.Should().NotContain(e => e.Action == "SomeoneElsesCommand");
    }
}
