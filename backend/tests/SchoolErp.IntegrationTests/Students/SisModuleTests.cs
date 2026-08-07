using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Application.Students.Queries;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Students;

/// <summary>
/// Boots the full composition against PostgreSQL and seeds two schools, each
/// with an academic year and class structure, so SIS behavior AND cross-tenant
/// isolation can be exercised through the real CQRS pipeline.
/// </summary>
public sealed class SisModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_sis_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid SchoolA { get; } = Guid.NewGuid();

    public Guid SchoolB { get; } = Guid.NewGuid();

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
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.AddRange(
                new Tenant { Id = SchoolA, Code = "SISA01", Name = "School A", Subdomain = "sis-a", Status = TenantStatus.Active },
                new Tenant { Id = SchoolB, Code = "SISB01", Name = "School B", Subdomain = "sis-b", Status = TenantStatus.Active });
            await db.SaveChangesAsync();
        }

        // Each school gets its own year + "Grade 5 A/B" via the real commands.
        foreach (var tenantId in new[] { SchoolA, SchoolB })
        {
            await using var scope = CreateScope(tenantId);
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateAcademicYearCommand(
                "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
            await sender.Send(new CreateClassCommand("Grade 5", 5, ["A", "B"]));
        }
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Creates a scope with the tenant already bound (as middleware would).</summary>
    public AsyncServiceScope CreateScope(Guid tenantId)
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        return scope;
    }
}

/// <summary>SIS behavior through the full CQRS pipeline.</summary>
public sealed class SisModuleTests : IClassFixture<SisModuleFixture>
{
    private readonly SisModuleFixture _fixture;

    public SisModuleTests(SisModuleFixture fixture) => _fixture = fixture;

    private static async Task<(Guid YearId, Guid ClassId, Guid SectionAId)> GetStructureAsync(ISender sender)
    {
        var years = await sender.Send(new GetAcademicYearsQuery());
        var classes = await sender.Send(new GetClassesQuery());
        var grade5 = classes.Single(c => c.Name == "Grade 5");
        return (years.Single().Id, grade5.Id, grade5.Sections.Single(s => s.Name == "A").Id);
    }

    private static AdmitStudentCommand NewAdmission(
        Guid yearId, Guid classId, Guid sectionId,
        string firstName = "Aarav", string guardianPhone = "+919812345670") => new(
        AdmissionNumber: null,
        FirstName: firstName,
        LastName: "Sharma",
        DateOfBirth: new DateOnly(2016, 4, 12),
        Gender: Gender.Male,
        BloodGroup: "B+",
        Email: null,
        Phone: null,
        AddressLine1: "12 MG Road",
        City: "Hyderabad",
        State: "Telangana",
        PostalCode: "500001",
        MedicalNotes: null,
        AdmissionDate: new DateOnly(2026, 6, 10),
        AcademicYearId: yearId,
        SchoolClassId: classId,
        SectionId: sectionId,
        RollNumber: null,
        Guardians:
        [
            new GuardianInput("Rakesh", "Sharma", GuardianRelation.Father, guardianPhone, null, "Engineer", IsPrimary: true),
        ]);

    [Fact]
    public async Task Admission_creates_student_guardian_and_enrollment_with_generated_number()
    {
        await using var scope = _fixture.CreateScope(_fixture.SchoolA);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (yearId, classId, sectionId) = await GetStructureAsync(sender);

        var studentId = await sender.Send(NewAdmission(yearId, classId, sectionId));

        var detail = await sender.Send(new GetStudentByIdQuery(studentId));
        detail.AdmissionNumber.Should().MatchRegex(@"^ADM-2026-\d{4}$");
        detail.Guardians.Should().ContainSingle(g => g.IsPrimary && g.FirstName == "Rakesh");
        detail.CurrentEnrollment.Should().NotBeNull();
        detail.CurrentEnrollment!.ClassName.Should().Be("Grade 5");
        detail.CurrentEnrollment.SectionName.Should().Be("A");
    }

    [Fact]
    public async Task Sibling_admission_reuses_the_guardian_matched_by_phone()
    {
        await using var scope = _fixture.CreateScope(_fixture.SchoolA);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (yearId, classId, sectionId) = await GetStructureAsync(sender);

        const string phone = "+919899000011";
        var first = await sender.Send(NewAdmission(yearId, classId, sectionId, "Isha", phone));
        var second = await sender.Send(NewAdmission(yearId, classId, sectionId, "Vihaan", phone));

        var guardianIds = await db.StudentGuardians
            .Where(sg => sg.StudentId == first || sg.StudentId == second)
            .Select(sg => sg.GuardianId)
            .ToListAsync();

        guardianIds.Should().HaveCount(2);
        guardianIds.Distinct().Should().HaveCount(1, "siblings share one guardian record");
    }

    [Fact]
    public async Task Students_list_filters_by_class_and_search()
    {
        await using var scope = _fixture.CreateScope(_fixture.SchoolA);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (yearId, classId, sectionId) = await GetStructureAsync(sender);

        await sender.Send(NewAdmission(yearId, classId, sectionId, "Meera", "+919899000022"));

        var byClass = await sender.Send(new GetStudentsQuery(SchoolClassId: classId));
        byClass.Items.Should().Contain(s => s.FirstName == "Meera");
        byClass.Items.Should().OnlyContain(s => s.ClassName == "Grade 5");

        var bySearch = await sender.Send(new GetStudentsQuery(Search: "meera"));
        bySearch.Items.Should().ContainSingle(s => s.FirstName == "Meera");
    }

    [Fact]
    public async Task Students_are_invisible_across_tenants()
    {
        await using (var scopeA = _fixture.CreateScope(_fixture.SchoolA))
        {
            var senderA = scopeA.ServiceProvider.GetRequiredService<ISender>();
            var (yearId, classId, sectionId) = await GetStructureAsync(senderA);
            await senderA.Send(NewAdmission(yearId, classId, sectionId, "Advait", "+919899000033"));
        }

        await using var scopeB = _fixture.CreateScope(_fixture.SchoolB);
        var senderB = scopeB.ServiceProvider.GetRequiredService<ISender>();
        var result = await senderB.Send(new GetStudentsQuery(Search: "Advait"));

        result.TotalCount.Should().Be(0, "School B must never see School A's students");
    }

    [Fact]
    public async Task Admission_into_a_section_of_another_class_is_rejected()
    {
        await using var scope = _fixture.CreateScope(_fixture.SchoolA);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (yearId, classId, _) = await GetStructureAsync(sender);

        var act = () => sender.Send(NewAdmission(yearId, classId, Guid.NewGuid(), "Rogue", "+919899000044"));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Duplicate_explicit_admission_number_conflicts()
    {
        await using var scope = _fixture.CreateScope(_fixture.SchoolA);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (yearId, classId, sectionId) = await GetStructureAsync(sender);

        var command = NewAdmission(yearId, classId, sectionId, "Kiara", "+919899000055")
            with { AdmissionNumber = "CUSTOM-1" };
        await sender.Send(command);

        var duplicate = NewAdmission(yearId, classId, sectionId, "Riya", "+919899000066")
            with { AdmissionNumber = "CUSTOM-1" };
        var act = () => sender.Send(duplicate);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*CUSTOM-1*");
    }
}
