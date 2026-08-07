using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Attendance.Commands;
using SchoolErp.Application.Attendance.Queries;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Notifications;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Auth;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Attendance;

/// <summary>
/// One school, one year, one class with two students — enough to exercise
/// marking, re-marking, the absence outbox, and the month calendar.
/// </summary>
public sealed class AttendanceModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_att_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid SectionId { get; private set; }

    public Guid EnrollmentA { get; private set; }

    public Guid EnrollmentB { get; private set; }

    public Guid StudentA { get; private set; }

    public Guid StudentB { get; private set; }

    /// <summary>Captures SMS handed to the gateway by the outbox processor.</summary>
    public RecordingSmsSender SmsSender { get; } = new();

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
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        services.AddSingleton<ISmsSender>(SmsSender);
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "ATTN01",
                Name = "Attendance Test School",
                Subdomain = "attend",
                Status = TenantStatus.Active,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateAcademicYearCommand(
                "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
            var schoolClass = await sender.Send(new CreateClassCommand("Grade 6", 6, ["A"]));
            SectionId = schoolClass.Sections.Single().Id;
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

            StudentA = await AdmitAsync(sender, yearId, "Ravi", "+919700000001", 1);
            StudentB = await AdmitAsync(sender, yearId, "Sita", "+919700000002", 2);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnrollmentA = await db.Enrollments.Where(e => e.StudentId == StudentA)
                .Select(e => e.Id).SingleAsync();
            EnrollmentB = await db.Enrollments.Where(e => e.StudentId == StudentB)
                .Select(e => e.Id).SingleAsync();
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

    /// <summary>Scope with NO tenant — how the outbox dispatcher runs.</summary>
    public AsyncServiceScope CreateDispatcherScope() => _provider.CreateAsyncScope();

    private async Task<Guid> AdmitAsync(
        ISender sender, Guid yearId, string firstName, string guardianPhone, int roll)
    {
        var schoolClass = (await sender.Send(new GetClassesQuery())).Single();
        return await sender.Send(new AdmitStudentCommand(
            null, firstName, "Kumar", new DateOnly(2015, 1, 15), Gender.Male,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), yearId, schoolClass.Id, SectionId, roll,
            [new GuardianInput("Guardian", "Kumar", GuardianRelation.Father, guardianPhone, null, null, true)]));
    }
}

/// <summary>Attendance behavior through the full pipeline, outbox included.</summary>
public sealed class AttendanceModuleTests : IClassFixture<AttendanceModuleFixture>
{
    private readonly AttendanceModuleFixture _fixture;

    public AttendanceModuleTests(AttendanceModuleFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Marking_persists_and_the_grid_reflects_it()
    {
        var date = new DateOnly(2026, 7, 1);
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new MarkAttendanceCommand(_fixture.SectionId, date,
        [
            new AttendanceEntry(_fixture.EnrollmentA, AttendanceStatus.Present, null),
            new AttendanceEntry(_fixture.EnrollmentB, AttendanceStatus.Late, "Bus delay"),
        ]));

        var grid = await sender.Send(new GetSectionAttendanceQuery(_fixture.SectionId, date));
        grid.IsMarked.Should().BeTrue();
        grid.Roster.Should().HaveCount(2);
        grid.Roster.Single(r => r.StudentId == _fixture.StudentA).Status
            .Should().Be(AttendanceStatus.Present);
        grid.Roster.Single(r => r.StudentId == _fixture.StudentB).Remarks.Should().Be("Bus delay");
    }

    [Fact]
    public async Task Remarking_updates_in_place_without_duplicates()
    {
        var date = new DateOnly(2026, 7, 2);
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await sender.Send(new MarkAttendanceCommand(_fixture.SectionId, date,
            [new AttendanceEntry(_fixture.EnrollmentA, AttendanceStatus.Absent, null)]));
        await sender.Send(new MarkAttendanceCommand(_fixture.SectionId, date,
            [new AttendanceEntry(_fixture.EnrollmentA, AttendanceStatus.Present, "Arrived late")]));

        var records = await db.AttendanceRecords
            .Where(a => a.EnrollmentId == _fixture.EnrollmentA && a.Date == date)
            .ToListAsync();
        records.Should().ContainSingle().Which.Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task Absence_queues_an_outbox_sms_and_the_dispatcher_delivers_it()
    {
        var date = new DateOnly(2026, 7, 3);

        await using (var scope = _fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new MarkAttendanceCommand(_fixture.SectionId, date,
                [new AttendanceEntry(_fixture.EnrollmentB, AttendanceStatus.Absent, null)]));

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var queued = await db.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.TenantId == _fixture.TenantId)
                .ToListAsync();
            // System.Text.Json unicode-escapes '+' in the stored JSON, so match digits only.
            queued.Should().ContainSingle("one absence → one SMS")
                .Which.Payload.Should().Contain("919700000002");
        }

        // The dispatcher runs WITHOUT a tenant scope — this also proves the
        // outbox is reachable outside RLS while business tables are not.
        await using (var dispatcherScope = _fixture.CreateDispatcherScope())
        {
            var processor = dispatcherScope.ServiceProvider.GetRequiredService<OutboxProcessor>();
            var processed = await processor.ProcessPendingAsync();
            processed.Should().BeGreaterThan(0);
        }

        _fixture.SmsSender.Sent.Should().Contain(s =>
            s.Phone == "+919700000002" && s.Message.Contains("Sita") && s.Message.Contains("absent"));
    }

    [Fact]
    public async Task Month_summary_computes_counters_and_percentage()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Four school days in August: P, P, A, L → 3 of 4 attended = 75%.
        var days = new (int Day, AttendanceStatus Status)[]
        {
            (3, AttendanceStatus.Present),
            (4, AttendanceStatus.Present),
            (5, AttendanceStatus.Absent),
            (6, AttendanceStatus.Late),
        };
        foreach (var (day, status) in days)
        {
            await sender.Send(new MarkAttendanceCommand(_fixture.SectionId, new DateOnly(2026, 8, day),
                [new AttendanceEntry(_fixture.EnrollmentA, status, null)]));
        }

        var month = await sender.Send(new GetStudentMonthAttendanceQuery(_fixture.StudentA, 2026, 8));
        month.MarkedDays.Should().Be(4);
        month.PresentCount.Should().Be(2);
        month.AbsentCount.Should().Be(1);
        month.LateCount.Should().Be(1);
        month.AttendancePercent.Should().Be(75.0);
    }

    [Fact]
    public async Task Marking_an_enrollment_outside_the_section_is_rejected()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new MarkAttendanceCommand(_fixture.SectionId, new DateOnly(2026, 7, 4),
            [new AttendanceEntry(Guid.NewGuid(), AttendanceStatus.Present, null)]));
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
