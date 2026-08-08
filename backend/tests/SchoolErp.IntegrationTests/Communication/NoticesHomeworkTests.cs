using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Communication;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Homework;
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

/// <summary>One school, two classes; a student in Grade 2 A.</summary>
public sealed class NoticesHomeworkFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_comm_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid Grade2Id { get; private set; }

    public Guid Grade2SectionA { get; private set; }

    public Guid Grade4Id { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid SubjectId { get; private set; }

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
                Code = "COMM01",
                Name = "Comm Test School",
                Subdomain = "commtest",
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

            var grade2 = await sender.Send(new CreateClassCommand("Grade 2", 2, ["A", "B"]));
            Grade2Id = grade2.Id;
            Grade2SectionA = grade2.Sections.Single(s => s.Name == "A").Id;
            var grade4 = await sender.Send(new CreateClassCommand("Grade 4", 4, ["A"]));
            Grade4Id = grade4.Id;

            SubjectId = (await sender.Send(new CreateSubjectCommand("English", "ENG"))).Id;

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Kavya", "Nair", new DateOnly(2019, 8, 15), Gender.Female,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, Grade2Id, Grade2SectionA, 1,
                [new GuardianInput("Guardian", "Nair", GuardianRelation.Mother, "+919300000001", null, null, true)]));
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

/// <summary>Notice and homework visibility rules.</summary>
public sealed class NoticesHomeworkTests : IClassFixture<NoticesHomeworkFixture>
{
    private readonly NoticesHomeworkFixture _fixture;

    public NoticesHomeworkTests(NoticesHomeworkFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Student_sees_school_wide_and_own_class_notices_but_not_other_classes()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateNoticeCommand("Holiday on Friday", "School closed.", null, null, true));
        await sender.Send(new CreateNoticeCommand("Grade 2 picnic", "Bring caps.", _fixture.Grade2Id, null, false));
        await sender.Send(new CreateNoticeCommand("Grade 4 trip", "Museum visit.", _fixture.Grade4Id, null, false));
        await sender.Send(new CreateNoticeCommand("Old notice", "Expired.", null, new DateOnly(2026, 1, 1), false));

        var visible = await sender.Send(new GetStudentNoticesQuery(
            _fixture.StudentId, new DateOnly(2026, 8, 6)));

        visible.Select(n => n.Title).Should()
            .Contain(["Holiday on Friday", "Grade 2 picnic"])
            .And.NotContain(["Grade 4 trip", "Old notice"]);
        visible[0].Title.Should().Be("Holiday on Friday", "pinned notices sort first");
    }

    [Fact]
    public async Task Student_sees_class_and_own_section_homework_only()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sectionB = await db.Sections
            .Where(s => s.SchoolClassId == _fixture.Grade2Id && s.Name == "B")
            .Select(s => s.Id)
            .SingleAsync();

        await sender.Send(new CreateHomeworkCommand(
            _fixture.Grade2Id, null, _fixture.SubjectId,
            "Reading", "Read chapter 4.", new DateOnly(2026, 8, 20)));
        await sender.Send(new CreateHomeworkCommand(
            _fixture.Grade2Id, _fixture.Grade2SectionA, _fixture.SubjectId,
            "Section A worksheet", "Solve page 12.", new DateOnly(2026, 8, 21)));
        await sender.Send(new CreateHomeworkCommand(
            _fixture.Grade2Id, sectionB, _fixture.SubjectId,
            "Section B worksheet", "Solve page 13.", new DateOnly(2026, 8, 21)));

        var visible = await sender.Send(new GetStudentHomeworkQuery(_fixture.StudentId));

        visible.Select(h => h.Title).Should()
            .Contain(["Reading", "Section A worksheet"])
            .And.NotContain("Section B worksheet");
    }

    [Fact]
    public async Task Homework_for_a_foreign_section_is_rejected()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new CreateHomeworkCommand(
            _fixture.Grade4Id, _fixture.Grade2SectionA, _fixture.SubjectId,
            "Wrong", "Section belongs to Grade 2.", new DateOnly(2026, 8, 22)));

        await act.Should().ThrowAsync<SchoolErp.Application.Common.Exceptions.NotFoundException>();
    }
}
