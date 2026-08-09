using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Attendance.Commands;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Staff;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Application.Timetable;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Staff;

/// <summary>Current user whose id the test can swap between teachers.</summary>
public sealed class SwitchableCurrentUser : ICurrentUser
{
    public string? UserId { get; set; } = Guid.NewGuid().ToString();

    public string? UserName => "Integration Test";

    public bool IsAuthenticated => true;
}

/// <summary>Clock pinned to a fixed instant so day-of-week logic is stable.</summary>
public sealed class FixedClock : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedClock(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>
/// Two teachers with their own timetable slots on the same day, so "my day"
/// can be proved to leak nothing across them. Wednesday 2026-08-12 is the
/// pinned "today" (ISO day 3).
/// </summary>
public sealed class TeacherDayFixture : IAsyncLifetime
{
    /// <summary>The pinned today — a Wednesday.</summary>
    public static readonly DateOnly Today = new(2026, 8, 12);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_teacherday_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public SwitchableCurrentUser CurrentUser { get; } = new();

    public Guid AnitaUserId { get; } = Guid.NewGuid();

    public Guid BhaskarUserId { get; } = Guid.NewGuid();

    public Guid AnitaId { get; private set; }

    public Guid BhaskarId { get; private set; }

    public Guid Grade7Id { get; private set; }

    public Guid Grade8Id { get; private set; }

    public Guid Grade7SectionId { get; private set; }

    public Guid Grade8SectionId { get; private set; }

    public Guid YearId { get; private set; }

    public Guid MathId { get; private set; }

    public Guid ScienceId { get; private set; }

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
        services.AddScoped<ICurrentUser>(_ => CurrentUser);
        // Registered last so it wins over Infrastructure's system clock.
        services.AddSingleton<TimeProvider>(
            new FixedClock(new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero)));
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDAY1",
                Name = "Teacher Day School",
                Subdomain = "tdaytest",
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
            YearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

            var grade7 = await sender.Send(new CreateClassCommand("Grade 7", 7, ["A"]));
            Grade7Id = grade7.Id;
            Grade7SectionId = grade7.Sections.Single().Id;
            var grade8 = await sender.Send(new CreateClassCommand("Grade 8", 8, ["A"]));
            Grade8Id = grade8.Id;
            Grade8SectionId = grade8.Sections.Single().Id;

            MathId = (await sender.Send(new CreateSubjectCommand("Mathematics", "MATH"))).Id;
            ScienceId = (await sender.Send(new CreateSubjectCommand("Science", "SCI"))).Id;

            AnitaId = await sender.Send(new CreateTeacherCommand(
                "EMP-201", "Anita Rao", "+919300000201", null, null, "Mathematics", null));
            BhaskarId = await sender.Send(new CreateTeacherCommand(
                "EMP-202", "Bhaskar Rao", "+919300000202", null, null, "Science", null));

            // Anita owns Grade 7 Wednesday; Bhaskar owns Grade 8 Wednesday.
            await sender.Send(new DefineTimetableCommand(Grade7Id, Grade7SectionId,
            [
                new TimetableEntryInput(3, 1, new TimeOnly(9, 0), new TimeOnly(9, 45),
                    MathId, AnitaId, null),
                new TimetableEntryInput(3, 2, new TimeOnly(9, 50), new TimeOnly(10, 35),
                    MathId, AnitaId, null),
            ]));
            await sender.Send(new DefineTimetableCommand(Grade8Id, Grade8SectionId,
            [
                new TimetableEntryInput(3, 1, new TimeOnly(9, 0), new TimeOnly(9, 45),
                    ScienceId, BhaskarId, null),
            ]));

            // One student in each section, so "students taught" is per-teacher.
            await sender.Send(new AdmitStudentCommand(
                null, "Meera", "Sharma", new DateOnly(2014, 4, 4), Gender.Female,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), YearId, Grade7Id, Grade7SectionId, 1,
                [new GuardianInput("Kavya", "Sharma", GuardianRelation.Mother,
                    "+919300000301", null, null, true)]));
            await sender.Send(new AdmitStudentCommand(
                null, "Rohit", "Nair", new DateOnly(2013, 5, 5), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), YearId, Grade8Id, Grade8SectionId, 1,
                [new GuardianInput("Latha", "Nair", GuardianRelation.Mother,
                    "+919300000302", null, null, true)]));
        }

        // Link each teacher to their sign-in account.
        await using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var anita = await db.Teachers.SingleAsync(t => t.Id == AnitaId);
            anita.UserId = AnitaUserId;
            var bhaskar = await db.Teachers.SingleAsync(t => t.Id == BhaskarId);
            bhaskar.UserId = BhaskarUserId;
            await db.SaveChangesAsync();
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

    /// <summary>Runs the query as the given signed-in user.</summary>
    public async Task<MyTeacherDayDto> DayAsAsync(Guid userId)
    {
        CurrentUser.UserId = userId.ToString();
        await using var scope = CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetMyTeacherDayQuery());
    }
}

/// <summary>The teacher's own day, and the wall around it.</summary>
public sealed class TeacherDayTests : IClassFixture<TeacherDayFixture>
{
    private readonly TeacherDayFixture _fixture;

    public TeacherDayTests(TeacherDayFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Teacher_sees_only_their_own_periods_and_students()
    {
        var anita = await _fixture.DayAsAsync(_fixture.AnitaUserId);

        anita.TeacherName.Should().Be("Anita Rao");
        anita.Date.Should().Be(TeacherDayFixture.Today);
        anita.Periods.Where(p => !p.IsSubstitution)
            .Should().HaveCount(2, "Anita has two Wednesday slots of her own")
            .And.OnlyContain(p => p.ClassName == "Grade 7",
                "another teacher's class must never appear as her own");
        anita.Periods.Select(p => p.Period).Should().BeInAscendingOrder();
        anita.StudentsTaught.Should().Be(1);

        var bhaskar = await _fixture.DayAsAsync(_fixture.BhaskarUserId);

        bhaskar.TeacherName.Should().Be("Bhaskar Rao");
        bhaskar.Periods.Where(p => !p.IsSubstitution).Should().OnlyContain(
            p => p.ClassName == "Grade 8", "he only ever teaches Grade 8 here");
        bhaskar.StudentsTaught.Should().Be(1);
    }

    [Fact]
    public async Task Unmarked_attendance_clears_once_the_roll_call_is_taken()
    {
        var before = await _fixture.DayAsAsync(_fixture.AnitaUserId);
        before.Periods.Where(p => p.ClassName == "Grade 7")
            .Should().OnlyContain(p => !p.AttendanceMarked, "Grade 7 A is unmarked");

        await using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var enrollment = await db.Enrollments
                .Where(e => e.SectionId == _fixture.Grade7SectionId &&
                            e.Status == EnrollmentStatus.Active)
                .Select(e => new { e.Id, e.StudentId })
                .SingleAsync();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new MarkAttendanceCommand(
                _fixture.Grade7SectionId, TeacherDayFixture.Today,
                [new AttendanceEntry(enrollment.Id, AttendanceStatus.Present, null)]));
        }

        var after = await _fixture.DayAsAsync(_fixture.AnitaUserId);
        after.Periods.Where(p => p.ClassName == "Grade 7")
            .Should().OnlyContain(p => p.AttendanceMarked);
        after.SectionsAwaitingAttendance.Should().Be(before.SectionsAwaitingAttendance - 1,
            "exactly one section stopped being a chase");

        // Marking Anita's section must not clear Bhaskar's own chase list.
        var bhaskar = await _fixture.DayAsAsync(_fixture.BhaskarUserId);
        bhaskar.SectionsAwaitingAttendance.Should().Be(1, "Grade 8 A is still unmarked");
    }

    [Fact]
    public async Task Substitution_moves_the_period_to_whoever_covers_it()
    {
        var anitaBefore = (await _fixture.DayAsAsync(_fixture.AnitaUserId)).Periods.Count;
        Guid slotId;
        await using (var scope = _fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var grid = await sender.Send(new GetTimetableQuery(
                _fixture.Grade8Id, _fixture.Grade8SectionId));
            slotId = grid.Single(e => e.DayOfWeek == 3 && e.Period == 1).Id;
            await sender.Send(new ApplySubstitutionsCommand(
                _fixture.BhaskarId, TeacherDayFixture.Today,
                [new SubstitutionInput(slotId, _fixture.AnitaId)]));
        }

        try
        {
            var anita = await _fixture.DayAsAsync(_fixture.AnitaUserId);
            anita.Periods.Should().HaveCount(anitaBefore + 1, "the cover is added to her day");
            anita.Periods.Should().ContainSingle(p => p.IsSubstitution)
                .Which.ClassName.Should().Be("Grade 8");

            var bhaskar = await _fixture.DayAsAsync(_fixture.BhaskarUserId);
            bhaskar.Periods.Should().BeEmpty("his only slot is covered by Anita today");
        }
        finally
        {
            // Shared fixture: hand the period back so test order can't matter.
            await using var cleanup = _fixture.CreateScope();
            var db = cleanup.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.TimetableSubstitutions
                .Where(x => x.Date == TeacherDayFixture.Today && x.TimetableEntryId == slotId)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Non_teacher_account_gets_a_404_rather_than_someone_elses_day()
    {
        var stranger = () => _fixture.DayAsAsync(Guid.NewGuid());
        await stranger.Should().ThrowAsync<NotFoundException>();
    }
}
