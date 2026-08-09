using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.TenantCatalog;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.TenantCatalog;

/// <summary>One school to brand.</summary>
public sealed class BrandingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_branding_test")
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
                ["Storage:RootPath"] = Path.Combine(Path.GetTempPath(), $"branding-test-{Guid.NewGuid():N}"),
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
        await db.Database.MigrateAsync();
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Code = "BRAND1",
            Name = "Branding Test School",
            Subdomain = "brandingtest",
            Status = TenantStatus.Active,
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

/// <summary>Logo upload + anonymous branding lookup.</summary>
public sealed class BrandingTests : IClassFixture<BrandingFixture>
{
    // A valid 1×1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly BrandingFixture _fixture;

    public BrandingTests(BrandingFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Logo_upload_replaces_the_old_file_and_branding_is_public_by_code()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var firstUrl = await sender.Send(
            new UploadTenantLogoCommand(_fixture.TenantId, ".png", TinyPng));
        firstUrl.Should().StartWith("/api/v1/files/")
            .And.Contain(_fixture.TenantId.ToString("N"), "the file must live under the TARGET school");

        // Replacing deletes the first file so storage stays clean.
        var secondUrl = await sender.Send(
            new UploadTenantLogoCommand(_fixture.TenantId, ".png", TinyPng));
        secondUrl.Should().NotBe(firstUrl);
        (await storage.OpenAsync(firstUrl["/api/v1/files/".Length..])).Should().BeNull();
        (await storage.OpenAsync(secondUrl["/api/v1/files/".Length..])).Should().NotBeNull();

        // The anonymous lookup is case-insensitive and carries the theme.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == _fixture.TenantId);
        tenant.ThemePrimaryColor = "#00695C";
        await db.SaveChangesAsync();

        var branding = await sender.Send(new GetTenantBrandingQuery("brand1"));
        branding.Name.Should().Be("Branding Test School");
        branding.LogoUrl.Should().Be(secondUrl);
        branding.ThemePrimaryColor.Should().Be("#00695C");

        var unknown = () => sender.Send(new GetTenantBrandingQuery("NOPE99"));
        await unknown.Should().ThrowAsync<NotFoundException>();
    }
}
