using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Inventory;
using SchoolErp.Domain.Inventory;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Inventory;

/// <summary>A school with an empty store.</summary>
public sealed class InventoryFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_inventory_test")
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
            Code = "INV01",
            Name = "Inventory School",
            Subdomain = "invtest",
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

/// <summary>Store register behaviour: balances, the stock floor, low stock.</summary>
public sealed class InventoryModuleTests : IClassFixture<InventoryFixture>
{
    private readonly InventoryFixture _fixture;

    public InventoryModuleTests(InventoryFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Receipts_and_issues_move_the_running_balance()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var item = await sender.Send(new CreateInventoryItemCommand(
            "Chemistry gloves", "Lab", "pair", ReorderLevel: 10, UnitCost: 25m));
        item.QuantityOnHand.Should().Be(0);
        item.IsLow.Should().BeTrue("zero is at or below the reorder level");

        var received = await sender.Send(new RecordStockMovementCommand(
            item.Id, StockMovementKind.Receipt, 100, "Sharma Scientific", null, null));
        received.BalanceAfter.Should().Be(100);

        var issued = await sender.Send(new RecordStockMovementCommand(
            item.Id, StockMovementKind.Issue, 30, "Grade 9 lab", null, null));
        issued.BalanceAfter.Should().Be(70);

        var written = await sender.Send(new RecordStockMovementCommand(
            item.Id, StockMovementKind.WriteOff, 5, null, "Torn packet", null));
        written.BalanceAfter.Should().Be(65);

        // A physical count overrides the running total outright.
        var counted = await sender.Send(new RecordStockMovementCommand(
            item.Id, StockMovementKind.Adjustment, 60, null, "Annual count", null));
        counted.BalanceAfter.Should().Be(60);

        var items = await sender.Send(new GetInventoryItemsQuery("gloves"));
        items.Should().ContainSingle().Which.QuantityOnHand.Should().Be(60);

        var register = await sender.Send(new GetStockMovementsQuery(item.Id));
        register.Should().HaveCount(4, "the register is append-only");
        register.Select(m => m.BalanceAfter).Should().Contain([100, 70, 65, 60]);
    }

    [Fact]
    public async Task Stock_cannot_go_negative_and_names_stay_unique()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var item = await sender.Send(new CreateInventoryItemCommand(
            "Football", "Sports", "piece", ReorderLevel: 2, UnitCost: null));
        await sender.Send(new RecordStockMovementCommand(
            item.Id, StockMovementKind.Receipt, 5, null, null, null));

        var overIssue = () => sender.Send(new RecordStockMovementCommand(
            item.Id, StockMovementKind.Issue, 6, "Sports day", null, null));
        await overIssue.Should().ThrowAsync<ConflictException>()
            .WithMessage("*5 piece*");

        // The failed issue must not have moved anything.
        (await sender.Send(new GetInventoryItemsQuery("Football")))
            .Single().QuantityOnHand.Should().Be(5);

        var duplicate = () => sender.Send(new CreateInventoryItemCommand(
            "Football", null, "piece", 0, null));
        await duplicate.Should().ThrowAsync<ConflictException>();

        var negative = () => sender.Send(new RecordStockMovementCommand(
            item.Id, StockMovementKind.Receipt, -1, null, null, null));
        await negative.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Low_stock_view_lists_only_what_needs_reordering()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var plenty = await sender.Send(new CreateInventoryItemCommand(
            "Notebook", "Stationery", "piece", ReorderLevel: 20, UnitCost: 40m));
        await sender.Send(new RecordStockMovementCommand(
            plenty.Id, StockMovementKind.Receipt, 500, null, null, null));

        var scarce = await sender.Send(new CreateInventoryItemCommand(
            "Chalk box", "Stationery", "box", ReorderLevel: 15, UnitCost: 60m));
        await sender.Send(new RecordStockMovementCommand(
            scarce.Id, StockMovementKind.Receipt, 12, null, null, null));

        var low = await sender.Send(new GetInventoryItemsQuery(null, LowOnly: true));
        low.Should().Contain(i => i.Id == scarce.Id)
            .And.NotContain(i => i.Id == plenty.Id);

        // Retiring an item takes it out of the store entirely.
        await sender.Send(new UpdateInventoryItemCommand(
            scarce.Id, "Chalk box", "Stationery", "box", 15, 60m, IsActive: false));

        (await sender.Send(new GetInventoryItemsQuery(null, LowOnly: true)))
            .Should().NotContain(i => i.Id == scarce.Id, "retired items are not reordered");

        var retiredMove = () => sender.Send(new RecordStockMovementCommand(
            scarce.Id, StockMovementKind.Receipt, 5, null, null, null));
        await retiredMove.Should().ThrowAsync<ConflictException>();
    }
}
