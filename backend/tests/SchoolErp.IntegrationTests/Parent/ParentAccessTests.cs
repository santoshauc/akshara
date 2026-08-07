using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Parent;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Parent;

/// <summary>
/// Two families in one school: proves the parent access layer only ever
/// exposes a parent's own children, matched by phone.
/// </summary>
public sealed class ParentAccessFixture : IAsyncLifetime
{
    public const string ParentAPhone = "+919400000001";
    public const string ParentBPhone = "+919400000002";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_parent_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid ChildOfA { get; private set; }

    public Guid SiblingOfA { get; private set; }

    public Guid ChildOfB { get; private set; }

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
                Code = "PRNT01",
                Name = "Parent Test School",
                Subdomain = "parenttest",
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
            var grade1 = await sender.Send(new CreateClassCommand("Grade 1", 1, ["A"]));
            var sectionId = grade1.Sections.Single().Id;

            ChildOfA = await AdmitAsync(sender, yearId, grade1.Id, sectionId, "Asha", ParentAPhone, 1);
            SiblingOfA = await AdmitAsync(sender, yearId, grade1.Id, sectionId, "Arjun", ParentAPhone, 2);
            ChildOfB = await AdmitAsync(sender, yearId, grade1.Id, sectionId, "Bala", ParentBPhone, 3);
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

    private static async Task<Guid> AdmitAsync(
        ISender sender, Guid yearId, Guid classId, Guid sectionId,
        string firstName, string guardianPhone, int roll) =>
        await sender.Send(new AdmitStudentCommand(
            null, firstName, "Rao", new DateOnly(2020, 5, 1), Gender.Female,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), yearId, classId, sectionId, roll,
            [new GuardianInput("Parent", "Rao", GuardianRelation.Father, guardianPhone, null, null, true)]));
}

/// <summary>Parent access rules through the full pipeline.</summary>
public sealed class ParentAccessTests : IClassFixture<ParentAccessFixture>
{
    private readonly ParentAccessFixture _fixture;

    public ParentAccessTests(ParentAccessFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Parent_sees_exactly_their_own_children()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var children = await sender.Send(new GetMyChildrenQuery(
            UserId: null, UserPhone: ParentAccessFixture.ParentAPhone));

        children.Should().HaveCount(2, "parent A has two children (siblings share one guardian)");
        children.Select(c => c.StudentId).Should()
            .BeEquivalentTo([_fixture.ChildOfA, _fixture.SiblingOfA]);
        children.Should().OnlyContain(c => c.ClassName == "Grade 1" && c.SectionName == "A");
    }

    [Fact]
    public async Task Parent_cannot_reach_another_familys_child()
    {
        await using var scope = _fixture.CreateScope();
        var access = scope.ServiceProvider.GetRequiredService<ParentAccess>();

        var act = () => access.EnsureChildAsync(
            null, ParentAccessFixture.ParentAPhone, _fixture.ChildOfB, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>(
            "foreign children must look nonexistent, not forbidden");
    }

    [Fact]
    public async Task Unknown_phone_has_no_children()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var children = await sender.Send(new GetMyChildrenQuery(null, "+919999999999"));
        children.Should().BeEmpty();
    }
}
