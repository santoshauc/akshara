using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Admissions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Dashboard;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Admissions;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Admissions;

/// <summary>One school with a current year and a class to admit into.</summary>
public sealed class AdmissionsFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_admissions_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid YearId { get; private set; }

    public Guid ClassId { get; private set; }

    public Guid SectionId { get; private set; }

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
                Code = "ADMIS1",
                Name = "Admissions Test School",
                Subdomain = "admissionstest",
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
            var schoolClass = await sender.Send(new CreateClassCommand("Grade 1", 1, ["A"]));
            YearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
            ClassId = schoolClass.Id;
            SectionId = schoolClass.Sections.Single().Id;
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

/// <summary>D2: enquiry pipeline — capture, work, convert, and dashboard tiles.</summary>
public sealed class AdmissionsModuleTests : IClassFixture<AdmissionsFixture>
{
    private readonly AdmissionsFixture _fixture;

    public AdmissionsModuleTests(AdmissionsFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Enquiry_moves_through_the_pipeline_and_converts_into_a_student()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var enquiryId = await sender.Send(new CreateEnquiryCommand(
            "Kiran Kumar", new DateOnly(2020, 4, 12), "Grade 1", "Suresh Kumar",
            "+919600000200", "suresh@example.com", EnquirySource.WalkIn,
            FollowUpOn: new DateOnly(2026, 8, 1), Notes: "Asked about transport"));

        // Overdue follow-up floats to the top and is flagged.
        var board = await sender.Send(new GetEnquiriesQuery(null));
        var row = board.Should().ContainSingle(e => e.Id == enquiryId).Subject;
        row.Status.Should().Be(EnquiryStatus.New);
        row.FollowUpDue.Should().BeTrue("2026-08-01 is in the past");
        board[0].Id.Should().Be(enquiryId);

        // Work the pipeline: contacted, follow-up pushed out.
        await sender.Send(new UpdateEnquiryCommand(
            enquiryId, EnquiryStatus.Contacted, new DateOnly(2027, 1, 15), "Visit planned"));
        (await sender.Send(new GetEnquiriesQuery(EnquiryStatus.Contacted)))
            .Should().ContainSingle(e => e.Id == enquiryId)
            .Which.FollowUpDue.Should().BeFalse();

        // Directly setting Admitted is refused — conversion owns that stamp.
        var illegal = () => sender.Send(new UpdateEnquiryCommand(
            enquiryId, EnquiryStatus.Admitted, null, null));
        await illegal.Should().ThrowAsync<Exception>();

        // Admit for real, then convert.
        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Kiran", "Kumar", new DateOnly(2020, 4, 12), Gender.Male,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 8, 10), _fixture.YearId, _fixture.ClassId, _fixture.SectionId, 1,
            [new GuardianInput("Suresh", "Kumar", GuardianRelation.Father, "+919600000200", null, null, true)]));
        await sender.Send(new ConvertEnquiryCommand(enquiryId, studentId));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var converted = await db.AdmissionEnquiries.SingleAsync(e => e.Id == enquiryId);
        converted.Status.Should().Be(EnquiryStatus.Admitted);
        converted.StudentId.Should().Be(studentId);
        converted.FollowUpOn.Should().BeNull();

        // Converting twice is refused.
        var again = () => sender.Send(new ConvertEnquiryCommand(enquiryId, studentId));
        await again.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Public_website_enquiries_land_in_the_crm_without_duplicating_open_ones()
    {
        // No tenant scope — the command resolves the school by code itself.
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new SubmitPublicEnquiryCommand(
            "admis1", "Web Child", null, "Grade 1", "Web Parent",
            "+919600000400", "web@example.com", "From the website form"));
        // Resubmitting the same phone while the enquiry is open is a no-op.
        await sender.Send(new SubmitPublicEnquiryCommand(
            "ADMIS1", "Web Child", null, "Grade 1", "Web Parent",
            "+919600000400", null, null));

        var board = await sender.Send(new GetEnquiriesQuery(null));
        var mine = board.Where(e => e.Phone == "+919600000400").ToList();
        mine.Should().ContainSingle("open enquiries must not duplicate")
            .Which.Should().Match<EnquiryDto>(e =>
                e.Source == EnquirySource.Website &&
                e.Status == EnquiryStatus.New &&
                e.ChildName == "Web Child");

        var unknownSchool = () => sender.Send(new SubmitPublicEnquiryCommand(
            "NOPE99", "X", null, "Grade 1", "Y", "+919600000401", null, null));
        await unknownSchool.Should().ThrowAsync<NotFoundException>(
            "the API layer converts this to a probe-safe 202");
    }

    [Fact]
    public async Task Dashboard_counts_open_enquiries_and_due_follow_ups()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var before = await sender.Send(new GetDashboardQuery());

        var dueId = await sender.Send(new CreateEnquiryCommand(
            "Meena Iyer", null, "Grade 1", "Raghav Iyer", "+919600000201", null,
            EnquirySource.Website, FollowUpOn: new DateOnly(2026, 1, 1), Notes: null));
        var lostId = await sender.Send(new CreateEnquiryCommand(
            "Rohit Das", null, "Grade 1", "Amit Das", "+919600000202", null,
            EnquirySource.Referral, FollowUpOn: new DateOnly(2026, 1, 1), Notes: null));
        await sender.Send(new UpdateEnquiryCommand(lostId, EnquiryStatus.Lost, null, null));

        var after = await sender.Send(new GetDashboardQuery());
        // The lost enquiry drops out of both tiles; the due one counts in both.
        (after.OpenEnquiries - before.OpenEnquiries).Should().Be(1);
        (after.EnquiryFollowUpsDueToday - before.EnquiryFollowUpsDueToday).Should().Be(1);

        (await sender.Send(new GetEnquiriesQuery(EnquiryStatus.Lost)))
            .Should().Contain(e => e.Id == lostId)
            .Which.FollowUpDue.Should().BeFalse("lost enquiries have nothing to chase");
        _ = dueId;
    }
}
