using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Application.Timetable;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Timetable;

/// <summary>One school, one class with sections A/B, one student in A.</summary>
public sealed class TimetableModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_tt_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid ClassId { get; private set; }

    public Guid SectionAId { get; private set; }

    public Guid SectionBId { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid MathId { get; private set; }

    public Guid EnglishId { get; private set; }

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
                Code = "TIME01",
                Name = "Timetable Test School",
                Subdomain = "timetest",
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

            var grade9 = await sender.Send(new CreateClassCommand("Grade 9", 9, ["A", "B"]));
            ClassId = grade9.Id;
            SectionAId = grade9.Sections.Single(s => s.Name == "A").Id;
            SectionBId = grade9.Sections.Single(s => s.Name == "B").Id;

            MathId = (await sender.Send(new CreateSubjectCommand("Mathematics", "MATH"))).Id;
            EnglishId = (await sender.Send(new CreateSubjectCommand("English", "ENG"))).Id;

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Zara", "Khan", new DateOnly(2012, 12, 12), Gender.Female,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, ClassId, SectionAId, 1,
                [new GuardianInput("Parent", "Khan", GuardianRelation.Mother, "+919100000001", null, null, true)]));
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

/// <summary>Timetable define/publish/visibility rules.</summary>
public sealed class TimetableModuleTests : IClassFixture<TimetableModuleFixture>
{
    private readonly TimetableModuleFixture _fixture;

    public TimetableModuleTests(TimetableModuleFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Parents_see_nothing_until_published_then_the_published_grid()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Define a class-wide Monday: Math then English (drafts).
        await sender.Send(new DefineTimetableCommand(_fixture.ClassId, null,
        [
            new TimetableEntryInput(1, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, "Mrs. Rao"),
            new TimetableEntryInput(1, 2, new TimeOnly(8, 45), new TimeOnly(9, 30), _fixture.EnglishId, "Mr. Das"),
        ]));

        (await sender.Send(new GetStudentTimetableQuery(_fixture.StudentId)))
            .Should().BeEmpty("drafts are invisible to parents");

        // Staff sees the drafts.
        var staffView = await sender.Send(new GetTimetableQuery(_fixture.ClassId, null));
        staffView.Should().HaveCount(2).And.OnlyContain(e => !e.IsPublished);

        // Publish → the parent view lights up, ordered by day then period.
        await sender.Send(new PublishTimetableCommand(_fixture.ClassId, null));
        var published = await sender.Send(new GetStudentTimetableQuery(_fixture.StudentId));
        published.Should().HaveCount(2);
        published[0].SubjectName.Should().Be("Mathematics");
        published[0].StartTime.Should().Be(new TimeOnly(8, 0));
        published[1].TeacherName.Should().Be("Mr. Das");
    }

    [Fact]
    public async Task Section_scoped_entries_reach_only_that_section()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Section B gets a Tuesday period; our student is in Section A.
        await sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionBId,
            [new TimetableEntryInput(2, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, null)]));
        await sender.Send(new PublishTimetableCommand(_fixture.ClassId, _fixture.SectionBId));

        var visible = await sender.Send(new GetStudentTimetableQuery(_fixture.StudentId));
        visible.Should().NotContain(e => e.DayOfWeek == 2,
            "Section B's Tuesday period must not reach a Section A student");
    }

    [Fact]
    public async Task Duplicate_slots_and_inverted_times_are_rejected()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var duplicate = () => sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionAId,
        [
            new TimetableEntryInput(3, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, null),
            new TimetableEntryInput(3, 1, new TimeOnly(9, 0), new TimeOnly(9, 45), _fixture.EnglishId, null),
        ]));
        await duplicate.Should().ThrowAsync<FluentValidation.ValidationException>();

        var inverted = () => sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionAId,
            [new TimetableEntryInput(3, 1, new TimeOnly(9, 0), new TimeOnly(8, 0), _fixture.MathId, null)]));
        await inverted.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Redefining_resets_to_draft_until_republished()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionAId,
            [new TimetableEntryInput(4, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, null)]));
        await sender.Send(new PublishTimetableCommand(_fixture.ClassId, _fixture.SectionAId));

        // Staff redrafts the section timetable — parents must not see the draft.
        await sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionAId,
            [new TimetableEntryInput(4, 1, new TimeOnly(8, 30), new TimeOnly(9, 15), _fixture.EnglishId, null)]));

        var visible = await sender.Send(new GetStudentTimetableQuery(_fixture.StudentId));
        visible.Should().NotContain(e => e.DayOfWeek == 4, "redefined entries return to draft");
    }
}
