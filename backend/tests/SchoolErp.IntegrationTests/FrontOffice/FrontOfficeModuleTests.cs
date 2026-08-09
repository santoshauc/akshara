using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.FrontOffice;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.FrontOffice;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.FrontOffice;

/// <summary>One school with an enrolled student who has a primary guardian.</summary>
public sealed class FrontOfficeFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_frontoffice_test")
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
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "FO01",
                Name = "Front Office School",
                Subdomain = "fotest",
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
            var grade4 = await sender.Send(new CreateClassCommand("Grade 4", 4, ["A"]));

            StudentId = await sender.Send(new AdmitStudentCommand(
                null, "Kiran", "Reddy", new DateOnly(2016, 1, 20), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, grade4.Id, grade4.Sections.Single().Id, 1,
                [new GuardianInput("Sunita", "Reddy", GuardianRelation.Mother,
                    "+919400000001", null, null, true)]));
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

/// <summary>Gate register and early-release behaviour.</summary>
public sealed class FrontOfficeModuleTests : IClassFixture<FrontOfficeFixture>
{
    private readonly FrontOfficeFixture _fixture;

    public FrontOfficeModuleTests(FrontOfficeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Visitor_is_badged_on_arrival_and_closed_on_the_way_out()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var openBefore = (await sender.Send(new GetVisitorsQuery(null, OpenOnly: true))).Count;

        var entry = await sender.Send(new CheckInVisitorCommand(
            "Ramesh Gupta", "+919400000777", VisitorPurpose.ParentMeeting,
            "Class teacher", _fixture.StudentId, "Discussing progress"));

        entry.PassNumber.Should().MatchRegex(@"^V-\d{8}-\d{3}$");
        entry.CheckedOutAt.Should().BeNull("the visitor is still inside");
        entry.StudentName.Should().Be("Kiran Reddy");

        var openNow = await sender.Send(new GetVisitorsQuery(null, OpenOnly: true));
        openNow.Should().HaveCount(openBefore + 1)
            .And.Contain(v => v.Id == entry.Id);

        var closed = await sender.Send(new CheckOutVisitorCommand(entry.Id));
        closed.CheckedOutAt.Should().NotBeNull();

        // A second click must not overwrite the recorded exit time.
        var again = await sender.Send(new CheckOutVisitorCommand(entry.Id));
        again.CheckedOutAt.Should().Be(closed.CheckedOutAt);

        (await sender.Send(new GetVisitorsQuery(null, OpenOnly: true)))
            .Should().NotContain(v => v.Id == entry.Id);
    }

    [Fact]
    public async Task Gate_pass_numbers_run_in_sequence_and_alert_the_guardian()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var first = await sender.Send(new IssueGatePassCommand(
            _fixture.StudentId, "Dental appointment", "Sunita Reddy", "+919400000001"));
        first.PassNumber.Should().MatchRegex(@"^GP-\d{4}-\d{4}$");
        first.ReturnedAt.Should().BeNull();

        var second = await sender.Send(new IssueGatePassCommand(
            _fixture.StudentId, "Family function", "Sunita Reddy", null));
        var firstSeq = int.Parse(first.PassNumber[^4..], System.Globalization.CultureInfo.InvariantCulture);
        var secondSeq = int.Parse(second.PassNumber[^4..], System.Globalization.CultureInfo.InvariantCulture);
        secondSeq.Should().Be(firstSeq + 1, "pass numbers are sequential per year");

        // The guardian must be told their child left the premises.
        var outbox = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.TenantId == _fixture.TenantId)
            .Select(m => m.Payload)
            .ToListAsync();
        outbox.Should().Contain(p =>
            p.Contains("Kiran") && p.Contains(first.PassNumber) && p.Contains("919400000001"));

        var returned = await sender.Send(new MarkGatePassReturnedCommand(first.Id));
        returned.ReturnedAt.Should().NotBeNull();

        var today = await sender.Send(new GetGatePassesQuery(null));
        today.Should().Contain(p => p.Id == first.Id && p.ClassName == "Grade 4 A");
    }

    [Fact]
    public async Task Unknown_student_is_rejected_on_both_desks()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var badVisitor = () => sender.Send(new CheckInVisitorCommand(
            "Stranger", null, VisitorPurpose.Other, null, Guid.NewGuid(), null));
        await badVisitor.Should().ThrowAsync<NotFoundException>();

        var badPass = () => sender.Send(new IssueGatePassCommand(
            Guid.NewGuid(), "Reason", "Someone", null));
        await badPass.Should().ThrowAsync<NotFoundException>();

        var blankName = () => sender.Send(new CheckInVisitorCommand(
            "  ", null, VisitorPurpose.Other, null, null, null));
        await blankName.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
