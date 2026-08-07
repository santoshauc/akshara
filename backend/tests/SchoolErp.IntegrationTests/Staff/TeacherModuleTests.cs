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
using SchoolErp.Application.Staff;
using SchoolErp.Application.Timetable;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Staff;

/// <summary>One school with two classes and two subjects for clash scenarios.</summary>
public sealed class TeacherModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_staff_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid Class9Id { get; private set; }

    public Guid Class10Id { get; private set; }

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
                Code = "STAFF1",
                Name = "Staff Test School",
                Subdomain = "stafftest",
                Status = TenantStatus.Active,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Class9Id = (await sender.Send(new CreateClassCommand("Grade 9", 9, ["A"]))).Id;
            Class10Id = (await sender.Send(new CreateClassCommand("Grade 10", 10, ["A"]))).Id;
            MathId = (await sender.Send(new CreateSubjectCommand("Mathematics", "MATH"))).Id;
            EnglishId = (await sender.Send(new CreateSubjectCommand("English", "ENG"))).Id;
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

/// <summary>Teacher directory rules and timetable double-booking protection.</summary>
public sealed class TeacherModuleTests : IClassFixture<TeacherModuleFixture>
{
    private readonly TeacherModuleFixture _fixture;

    public TeacherModuleTests(TeacherModuleFixture fixture) => _fixture = fixture;

    private static TimetableEntryInput Slot(
        int day, int period, int startHour, int startMinute, int endHour, int endMinute,
        Guid subjectId, Guid? teacherId) =>
        new(day, period, new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute),
            subjectId, teacherId, null);

    [Fact]
    public async Task Teacher_crud_roundtrip_with_duplicate_protection()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var id = await sender.Send(new CreateTeacherCommand(
            "EMP-100", "Lakshmi Devi", "+919200000100", "lakshmi@school.test",
            "M.Sc., B.Ed.", "Mathematics", new DateOnly(2020, 6, 1)));

        (await sender.Send(new GetTeachersQuery("lakshmi")))
            .Should().ContainSingle(t => t.Id == id && t.IsActive);

        var duplicateCode = () => sender.Send(new CreateTeacherCommand(
            "EMP-100", "Someone Else", "+919200000199", null, null, null, null));
        await duplicateCode.Should().ThrowAsync<ConflictException>()
            .WithMessage("*EMP-100*");

        var duplicatePhone = () => sender.Send(new CreateTeacherCommand(
            "EMP-101", "Someone Else", "+919200000100", null, null, null, null));
        await duplicatePhone.Should().ThrowAsync<ConflictException>()
            .WithMessage("*+919200000100*");

        await sender.Send(new UpdateTeacherCommand(
            id, "Lakshmi Devi", "+919200000100", "lakshmi@school.test",
            "M.Sc., M.Ed.", "Mathematics", new DateOnly(2020, 6, 1), IsActive: false));
        (await sender.Send(new GetTeachersQuery("EMP-100")))
            .Single().Should().Match<TeacherDto>(t =>
                t.Qualification == "M.Sc., M.Ed." && !t.IsActive);
    }

    [Fact]
    public async Task Double_booked_teacher_is_rejected_across_classes_and_within_a_batch()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var teacherId = await sender.Send(new CreateTeacherCommand(
            "EMP-200", "Ravi Teja", "+919200000200", null, null, "Mathematics", null));

        // Ravi teaches Grade 9 Monday 8:00–8:45.
        await sender.Send(new DefineTimetableCommand(_fixture.Class9Id, null,
            [Slot(1, 1, 8, 0, 8, 45, _fixture.MathId, teacherId)]));

        // Grade 10 wants him Monday 8:30–9:15 — overlaps, must be rejected.
        var crossClass = () => sender.Send(new DefineTimetableCommand(_fixture.Class10Id, null,
            [Slot(1, 1, 8, 30, 9, 15, _fixture.MathId, teacherId)]));
        await crossClass.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Ravi Teja*Grade 9*");

        // A non-overlapping Grade 10 slot is fine.
        await sender.Send(new DefineTimetableCommand(_fixture.Class10Id, null,
            [Slot(1, 1, 9, 0, 9, 45, _fixture.MathId, teacherId)]));

        // Same-batch overlap: two overlapping Tuesday slots in one submission.
        var inBatch = () => sender.Send(new DefineTimetableCommand(_fixture.Class9Id, null,
        [
            Slot(2, 1, 8, 0, 8, 45, _fixture.MathId, teacherId),
            Slot(2, 2, 8, 30, 9, 15, _fixture.EnglishId, teacherId),
        ]));
        await inBatch.Should().ThrowAsync<ConflictException>()
            .WithMessage("*scheduled twice*");

        // Redefining the SAME scope he already occupies must not clash with itself.
        await sender.Send(new DefineTimetableCommand(_fixture.Class9Id, null,
            [Slot(1, 1, 8, 0, 8, 45, _fixture.EnglishId, teacherId)]));
    }

    [Fact]
    public async Task Inactive_teachers_cannot_be_scheduled()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var teacherId = await sender.Send(new CreateTeacherCommand(
            "EMP-300", "Retired Sir", "+919200000300", null, null, null, null));
        await sender.Send(new UpdateTeacherCommand(
            teacherId, "Retired Sir", "+919200000300", null, null, null, null, IsActive: false));

        var define = () => sender.Send(new DefineTimetableCommand(_fixture.Class9Id, null,
            [Slot(3, 1, 8, 0, 8, 45, _fixture.MathId, teacherId)]));
        await define.Should().ThrowAsync<ConflictException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task Teacher_schedule_spans_classes_and_resolves_names_in_grids()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var teacherId = await sender.Send(new CreateTeacherCommand(
            "EMP-400", "Anita Rao", "+919200000400", null, null, "English", null));

        await sender.Send(new DefineTimetableCommand(_fixture.Class9Id, null,
            [Slot(5, 1, 8, 0, 8, 45, _fixture.EnglishId, teacherId)]));
        await sender.Send(new DefineTimetableCommand(_fixture.Class10Id, null,
            [Slot(5, 2, 9, 0, 9, 45, _fixture.EnglishId, teacherId)]));

        var schedule = await sender.Send(new GetTeacherScheduleQuery(teacherId));
        schedule.Where(s => s.DayOfWeek == 5).Should().HaveCount(2);
        schedule.Select(s => s.ClassName).Should().Contain(["Grade 9", "Grade 10"]);

        // The class grid resolves the teacher's display name from the record.
        var grid = await sender.Send(new GetTimetableQuery(_fixture.Class9Id, null));
        grid.Single(e => e.DayOfWeek == 5).TeacherName.Should().Be("Anita Rao");
        grid.Single(e => e.DayOfWeek == 5).TeacherId.Should().Be(teacherId);
    }
}
