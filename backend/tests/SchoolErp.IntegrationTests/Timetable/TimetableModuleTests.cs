using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Application.Timetable;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Domain.Timetable;
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
            new TimetableEntryInput(1, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, null, "Mrs. Rao"),
            new TimetableEntryInput(1, 2, new TimeOnly(8, 45), new TimeOnly(9, 30), _fixture.EnglishId, null, "Mr. Das"),
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
            [new TimetableEntryInput(2, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, null, null)]));
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
            new TimetableEntryInput(3, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, null, null),
            new TimetableEntryInput(3, 1, new TimeOnly(9, 0), new TimeOnly(9, 45), _fixture.EnglishId, null, null),
        ]));
        await duplicate.Should().ThrowAsync<FluentValidation.ValidationException>();

        var inverted = () => sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionAId,
            [new TimetableEntryInput(3, 1, new TimeOnly(9, 0), new TimeOnly(8, 0), _fixture.MathId, null, null)]));
        await inverted.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task Redefining_resets_to_draft_until_republished()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionAId,
            [new TimetableEntryInput(4, 1, new TimeOnly(8, 0), new TimeOnly(8, 45), _fixture.MathId, null, null)]));
        await sender.Send(new PublishTimetableCommand(_fixture.ClassId, _fixture.SectionAId));

        // Staff redrafts the section timetable — parents must not see the draft.
        await sender.Send(new DefineTimetableCommand(_fixture.ClassId, _fixture.SectionAId,
            [new TimetableEntryInput(4, 1, new TimeOnly(8, 30), new TimeOnly(9, 15), _fixture.EnglishId, null, null)]));

        var visible = await sender.Send(new GetStudentTimetableQuery(_fixture.StudentId));
        visible.Should().NotContain(e => e.DayOfWeek == 4, "redefined entries return to draft");
    }

    [Fact]
    public async Task Recess_and_lunch_sit_between_the_periods_they_separate()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // A real Indian morning: two periods, recess, one more, lunch.
        // Deliberately submitted out of order — the day is ordered by clock.
        await sender.Send(new DefineTimetableCommand(_fixture.ClassId, null,
        [
            new TimetableEntryInput(5, null, new TimeOnly(11, 20), new TimeOnly(12, 0),
                null, null, null, TimetableSlotKind.Lunch, null),
            new TimetableEntryInput(5, 1, new TimeOnly(8, 0), new TimeOnly(8, 45),
                _fixture.MathId, null, null),
            new TimetableEntryInput(5, 3, new TimeOnly(9, 50), new TimeOnly(10, 35),
                _fixture.EnglishId, null, null),
            new TimetableEntryInput(5, null, new TimeOnly(9, 30), new TimeOnly(9, 50),
                null, null, null, TimetableSlotKind.Break, "Tiffin break"),
            new TimetableEntryInput(5, 2, new TimeOnly(8, 45), new TimeOnly(9, 30),
                _fixture.EnglishId, null, null),
        ]));
        await sender.Send(new PublishTimetableCommand(_fixture.ClassId, null));

        var day = (await sender.Send(new GetStudentTimetableQuery(_fixture.StudentId)))
            .Where(e => e.DayOfWeek == 5)
            .ToList();

        day.Select(e => e.SlotKind).Should().Equal(
            TimetableSlotKind.Lesson,
            TimetableSlotKind.Lesson,
            TimetableSlotKind.Break,
            TimetableSlotKind.Lesson,
            TimetableSlotKind.Lunch);

        var recess = day[2];
        recess.Label.Should().Be("Tiffin break", "the school's own wording reaches the parent");
        recess.Period.Should().BeNull("a break is not a numbered period");
        recess.SubjectId.Should().BeNull();
        recess.SubjectName.Should().BeNull();
        recess.TeacherId.Should().BeNull();

        // Lesson numbering is untouched by the breaks around it — period-wise
        // attendance stores these numbers.
        day.Where(e => e.SlotKind == TimetableSlotKind.Lesson)
            .Select(e => e.Period).Should().Equal(1, 2, 3);

        // A break with no label still renders; the apps supply the default.
        day[4].Label.Should().BeNull();
    }

    [Fact]
    public async Task A_break_cannot_be_taught_and_cannot_overlap_a_period()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var taughtLunch = () => sender.Send(new DefineTimetableCommand(
            _fixture.ClassId, _fixture.SectionAId,
            [
                new TimetableEntryInput(6, null, new TimeOnly(11, 0), new TimeOnly(11, 30),
                    _fixture.MathId, null, null, TimetableSlotKind.Lunch, null),
            ]));
        await taughtLunch.Should().ThrowAsync<FluentValidation.ValidationException>(
            "nobody teaches lunch");

        var numberedBreak = () => sender.Send(new DefineTimetableCommand(
            _fixture.ClassId, _fixture.SectionAId,
            [
                new TimetableEntryInput(6, 5, new TimeOnly(11, 0), new TimeOnly(11, 30),
                    null, null, null, TimetableSlotKind.Break, null),
            ]));
        await numberedBreak.Should().ThrowAsync<FluentValidation.ValidationException>(
            "a break is not a numbered period");

        // Lunch laid over a period. A ConflictException, not a validation
        // error: it is checked in the handler AFTER the teacher clash, so a
        // double-booked teacher still gets the message that names them.
        var overlapping = () => sender.Send(new DefineTimetableCommand(
            _fixture.ClassId, _fixture.SectionAId,
            [
                new TimetableEntryInput(6, 1, new TimeOnly(11, 0), new TimeOnly(11, 45),
                    _fixture.MathId, null, null),
                new TimetableEntryInput(6, null, new TimeOnly(11, 30), new TimeOnly(12, 0),
                    null, null, null, TimetableSlotKind.Lunch, null),
            ]));
        await overlapping.Should().ThrowAsync<ConflictException>()
            .WithMessage("*11:00*11:45*11:30*12:00*");

        // A lesson with no subject is still refused, breaks or not.
        var subjectless = () => sender.Send(new DefineTimetableCommand(
            _fixture.ClassId, _fixture.SectionAId,
            [
                new TimetableEntryInput(6, 1, new TimeOnly(11, 0), new TimeOnly(11, 45),
                    null, null, null),
            ]));
        await subjectless.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
