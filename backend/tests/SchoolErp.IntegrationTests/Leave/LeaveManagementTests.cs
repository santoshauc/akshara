using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Leave;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Leave;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Leave;

/// <summary>One school, one current year, one enrolled student.</summary>
public sealed class LeaveManagementFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_leave_test")
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
                Code = "LEAVE1",
                Name = "Leave Test School",
                Subdomain = "leavetest",
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
            var schoolClass = await sender.Send(new CreateClassCommand("Grade 4", 4, ["A"]));
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Divya", "Rao", new DateOnly(2017, 2, 10), Gender.Female,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, schoolClass.Id,
                schoolClass.Sections.Single().Id, 1,
                [new GuardianInput("Latha", "Rao", GuardianRelation.Mother, "+919700000100", null, null, true)]));
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

/// <summary>B3: submit → approve/reject flow with the attendance side effect.</summary>
public sealed class LeaveManagementTests : IClassFixture<LeaveManagementFixture>
{
    private readonly LeaveManagementFixture _fixture;

    public LeaveManagementTests(LeaveManagementFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Approving_a_student_request_marks_the_range_as_leave()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var from = new DateOnly(2026, 8, 10);
        var to = new DateOnly(2026, 8, 12);
        var leaveId = await sender.Send(new SubmitLeaveRequestCommand(
            _fixture.StudentId, from, to, "Family wedding"));

        // Pending, listed for staff and for the child.
        (await sender.Send(new GetLeaveRequestsQuery(LeaveRequestStatus.Pending)))
            .Should().Contain(l => l.Id == leaveId && l.ApplicantName == "Divya Rao");
        (await sender.Send(new GetStudentLeaveRequestsQuery(_fixture.StudentId)))
            .Should().ContainSingle(l => l.Id == leaveId);

        await sender.Send(new DecideLeaveRequestCommand(leaveId, Approve: true, "Enjoy"));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marks = await db.AttendanceRecords
            .Where(a => a.StudentId == _fixture.StudentId && a.Date >= from && a.Date <= to)
            .ToListAsync();
        marks.Should().HaveCount(3);
        marks.Should().OnlyContain(a => a.Status == AttendanceStatus.Leave);

        var leave = await db.LeaveRequests.SingleAsync(l => l.Id == leaveId);
        leave.Status.Should().Be(LeaveRequestStatus.Approved);
        leave.DecisionNote.Should().Be("Enjoy");
        leave.DecidedAt.Should().NotBeNull();

        // Deciding twice is refused.
        var again = () => sender.Send(new DecideLeaveRequestCommand(leaveId, false, null));
        await again.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Overlapping_requests_for_the_same_student_conflict()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new SubmitLeaveRequestCommand(
            _fixture.StudentId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), "Travel"));

        var overlap = () => sender.Send(new SubmitLeaveRequestCommand(
            _fixture.StudentId, new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 4), "More travel"));
        await overlap.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already covers*");
    }

    [Fact]
    public async Task Staff_requests_skip_attendance_and_list_under_mine()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var from = new DateOnly(2026, 10, 5);
        var leaveId = await sender.Send(new SubmitLeaveRequestCommand(
            null, from, from, "Medical appointment"));

        (await sender.Send(new GetMyLeaveRequestsQuery()))
            .Should().ContainSingle(l => l.Id == leaveId && l.Kind == LeaveApplicantKind.Staff);

        await sender.Send(new DecideLeaveRequestCommand(leaveId, Approve: true, null));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.AttendanceRecords.AnyAsync(a => a.Date == from))
            .Should().BeFalse("staff leave must not touch student attendance");
    }
}
