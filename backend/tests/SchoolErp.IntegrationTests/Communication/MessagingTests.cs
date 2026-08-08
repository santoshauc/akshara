using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Communication;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Communication;

/// <summary>One school with one enrolled student for the message thread.</summary>
public sealed class MessagingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_msg_test")
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
        services.AddScoped<GuidCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<GuidCurrentUser>());
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "MSGS01",
                Name = "Messaging Test School",
                Subdomain = "msgtest",
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
            var schoolClass = await sender.Send(new CreateClassCommand("Grade 2", 2, ["A"]));
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Zara", "Khan", new DateOnly(2019, 8, 1), Gender.Female,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, schoolClass.Id,
                schoolClass.Sections.Single().Id, 1,
                [new GuardianInput("Sana", "Khan", GuardianRelation.Mother, "+919700000200", null, null, true)]));
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

/// <summary>B8: the message loop with per-side read tracking.</summary>
public sealed class MessagingTests : IClassFixture<MessagingFixture>
{
    private readonly MessagingFixture _fixture;

    public MessagingTests(MessagingFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Parent_and_staff_converse_with_unread_counts()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Parent writes → the staff inbox shows 1 unread.
        await sender.Send(new SendStudentMessageCommand(
            _fixture.StudentId, "Zara will be late tomorrow, doctor's visit.", SentByStaff: false));

        var threads = await sender.Send(new GetMessageThreadsQuery());
        var thread = threads.Single(t => t.StudentId == _fixture.StudentId);
        thread.StudentName.Should().Be("Zara Khan");
        thread.UnreadForStaff.Should().Be(1);

        // Staff opens the thread → parent message marked read; inbox clears.
        var asStaff = await sender.Send(new GetStudentMessagesQuery(
            _fixture.StudentId, AsStaff: true));
        asStaff.Should().ContainSingle().Which.Read.Should().BeTrue();
        (await sender.Send(new GetMessageThreadsQuery()))
            .Single(t => t.StudentId == _fixture.StudentId)
            .UnreadForStaff.Should().Be(0);

        // Staff replies → the parent has 1 unread until they open the thread.
        await sender.Send(new SendStudentMessageCommand(
            _fixture.StudentId, "Noted, thank you for informing us!", SentByStaff: true));
        (await sender.Send(new GetUnreadForParentQuery(_fixture.StudentId))).Should().Be(1);

        var asParent = await sender.Send(new GetStudentMessagesQuery(
            _fixture.StudentId, AsStaff: false));
        asParent.Should().HaveCount(2);
        asParent[^1].SentByStaff.Should().BeTrue();
        (await sender.Send(new GetUnreadForParentQuery(_fixture.StudentId))).Should().Be(0);

        // Empty bodies are refused.
        var blank = () => sender.Send(new SendStudentMessageCommand(
            _fixture.StudentId, "   ", SentByStaff: false));
        await blank.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
