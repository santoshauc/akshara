using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.TenantCatalog.Commands;
using SchoolErp.Application.TenantCatalog.Queries;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.TenantCatalog;

/// <summary>
/// Boots the full Application + Infrastructure composition and drives the
/// tenant module through MediatR — validation pipeline included — against
/// real PostgreSQL.
/// </summary>
public sealed class TenantModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_module_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

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
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();
}

/// <summary>End-to-end tenant module behavior through the CQRS pipeline.</summary>
public sealed class TenantModuleTests : IClassFixture<TenantModuleFixture>
{
    private readonly TenantModuleFixture _fixture;

    public TenantModuleTests(TenantModuleFixture fixture) => _fixture = fixture;

    private static CreateTenantCommand NewSchool(string code, string subdomain) => new(
        Code: code,
        Name: $"School {code}",
        Subdomain: subdomain,
        CustomDomain: null,
        ContactEmail: null,
        ContactPhone: null,
        City: "Hyderabad",
        State: "Telangana",
        AffiliationBoard: "CBSE",
        Plan: SubscriptionPlan.Standard,
        EnabledModules: TenantModules.Core);

    [Fact]
    public async Task Create_then_query_roundtrips()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var created = await sender.Send(NewSchool("RTRIP1", "roundtrip"));
        created.Status.Should().Be(TenantStatus.Provisioning);

        var fetched = await sender.Send(new GetTenantByIdQuery(created.Id));
        fetched.Name.Should().Be("School RTRIP1");

        var listed = await sender.Send(new GetTenantsQuery(Search: "RTRIP1"));
        listed.TotalCount.Should().Be(1);
        listed.Items.Single().Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Create_with_duplicate_code_conflicts()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(NewSchool("DUPE01", "dupe-one"));

        var act = () => sender.Send(NewSchool("DUPE01", "dupe-two"));
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*code*");
    }

    [Fact]
    public async Task Invalid_command_is_rejected_by_the_pipeline_before_the_handler()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(NewSchool("bad", "UPPER CASE"));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Archived_school_cannot_be_reactivated()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var created = await sender.Send(NewSchool("ARCH01", "archived"));
        await sender.Send(new ChangeTenantStatusCommand(created.Id, TenantStatus.Archived));

        var act = () => sender.Send(new ChangeTenantStatusCommand(created.Id, TenantStatus.Active));
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
    }

    [Fact]
    public async Task Unknown_tenant_lookup_throws_not_found()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new GetTenantByIdQuery(Guid.NewGuid()));
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
