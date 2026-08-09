using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Admissions;
using SchoolErp.Application.Insights;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Admissions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Fees;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Insights;

/// <summary>One school, current year, one enrolled student.</summary>
public sealed class InsightsFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_insights_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid YearId { get; private set; }

    public Guid ClassId { get; private set; }

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
                Code = "INSGT1",
                Name = "Insights Test School",
                Subdomain = "insightstest",
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
            YearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
            ClassId = schoolClass.Id;

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Ishaan", "Gupta", new DateOnly(2019, 7, 20), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), YearId, ClassId,
                schoolClass.Sections.Single().Id, 1,
                [new GuardianInput("Rekha", "Gupta", GuardianRelation.Mother, "+919700000300", null, null, true)]));
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

/// <summary>D3: the management insights aggregates.</summary>
public sealed class InsightsModuleTests : IClassFixture<InsightsFixture>
{
    private readonly InsightsFixture _fixture;

    public InsightsModuleTests(InsightsFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Management_insights_aggregate_every_series()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var enrollment = await db.Enrollments
            .SingleAsync(e => e.StudentId == _fixture.StudentId);

        // Attendance: present today → 100% trend point and class row.
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            EnrollmentId = enrollment.Id,
            StudentId = _fixture.StudentId,
            SectionId = enrollment.SectionId,
            Date = today,
            Status = AttendanceStatus.Present,
        });

        // Fees: ₹1,000 due for the class, ₹400 paid this month → ₹600 outstanding.
        var head = new FeeHead { Name = "Tuition" };
        db.FeeHeads.Add(head);
        db.FeeStructureItems.Add(new FeeStructureItem
        {
            AcademicYearId = _fixture.YearId,
            SchoolClassId = _fixture.ClassId,
            FeeHeadId = head.Id,
            Amount = 1_000m,
            DueDate = today.AddMonths(1),
        });
        db.FeePayments.Add(new FeePayment
        {
            StudentId = _fixture.StudentId,
            AcademicYearId = _fixture.YearId,
            ReceiptNumber = "RCP-INS-1",
            Amount = 400m,
            PaidOn = today,
            Mode = PaymentMode.Cash,
        });

        // Exams: one published paper, 40/50 → 80% average.
        var exam = new Exam
        {
            Name = "Unit Test 1",
            AcademicYearId = _fixture.YearId,
            StartDate = today.AddDays(-7),
            EndDate = today.AddDays(-6),
            Status = ExamStatus.Published,
        };
        db.Exams.Add(exam);
        var paper = new ExamSubject
        {
            ExamId = exam.Id,
            SchoolClassId = _fixture.ClassId,
            SubjectId = (await AddSubjectAsync(db)).Id,
            MaxMarks = 50,
            PassMarks = 17,
        };
        db.ExamSubjects.Add(paper);
        db.MarkEntries.Add(new MarkEntry
        {
            ExamSubjectId = paper.Id,
            EnrollmentId = enrollment.Id,
            StudentId = _fixture.StudentId,
            MarksObtained = 40,
        });
        await db.SaveChangesAsync();

        // Enquiries: one open, one lost.
        await sender.Send(new CreateEnquiryCommand(
            "Tara Bose", null, "Grade 2", "Ravi Bose", "+919700000301", null,
            EnquirySource.Phone, null, null));
        var lostId = await sender.Send(new CreateEnquiryCommand(
            "Vivaan Shah", null, "Grade 2", "Nisha Shah", "+919700000302", null,
            EnquirySource.Website, null, null));
        await sender.Send(new UpdateEnquiryCommand(lostId, EnquiryStatus.Lost, null, null));

        var insights = await sender.Send(new GetManagementInsightsQuery());

        insights.AttendanceTrend.Should().ContainSingle(p => p.Date == today)
            .Which.Percent.Should().Be(100m);
        insights.FeeSeries.Should().HaveCount(6);
        insights.FeeSeries[^1].Collected.Should().Be(400m);
        insights.FeesOutstanding.Should().Be(600m);
        insights.ClassAttendance.Should().ContainSingle(c => c.ClassName == "Grade 2")
            .Which.Percent.Should().Be(100m);
        insights.ExamAverages.Should().ContainSingle(e => e.ExamName == "Unit Test 1")
            .Which.AveragePercent.Should().Be(80m);
        insights.EnquiryFunnel.New.Should().Be(1);
        insights.EnquiryFunnel.Lost.Should().Be(1);
        insights.SubstitutionsThisMonth.Should().Be(0);
    }

    private static async Task<SchoolErp.Domain.Academics.Subject> AddSubjectAsync(AppDbContext db)
    {
        var subject = new SchoolErp.Domain.Academics.Subject { Name = "Maths", Code = "MAT" };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return subject;
    }
}
