using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Staff;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Academics;

/// <summary>A college per test, so uniqueness rules are testable in isolation.</summary>
public sealed class CollegeStructureFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_college_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;
    private int _counter;

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

        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public async Task<Guid> NewCollegeAsync()
    {
        var n = Interlocked.Increment(ref _counter);
        var id = Guid.NewGuid();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = id,
            Code = $"COLL{n:D2}",
            Name = $"Test College {n}",
            Subdomain = $"coll{n}",
            InstitutionType = InstitutionType.College,
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>Adds a teacher so head-of-department can be exercised.</summary>
    public async Task<Guid> AddTeacherAsync(Guid tenantId, string code)
    {
        await using var scope = CreateScope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teacher = new Teacher
        {
            EmployeeCode = code,
            FullName = "Prof " + code,
            Phone = "+919000000000",
        };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();
        return teacher.Id;
    }

    public AsyncServiceScope CreateScope(Guid tenantId)
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        return scope;
    }
}

/// <summary>Departments, programmes, and the cohorts hanging off them.</summary>
public sealed class CollegeStructureTests : IClassFixture<CollegeStructureFixture>
{
    private readonly CollegeStructureFixture _fixture;

    public CollegeStructureTests(CollegeStructureFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Programmes_are_listed_under_their_department_with_the_head_named()
    {
        var tenantId = await _fixture.NewCollegeAsync();
        var headId = await _fixture.AddTeacherAsync(tenantId, "HOD-CSE");

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var deptId = await sender.Send(new CreateDepartmentCommand(
            "Computer Science", "cse", headId));
        await sender.Send(new CreateProgrammeCommand(
            deptId, "B.Tech Computer Science", "btcse",
            ProgrammeLevel.Undergraduate, 4, 2));
        await sender.Send(new CreateProgrammeCommand(
            deptId, "M.Tech Computer Science", "mtcse",
            ProgrammeLevel.Postgraduate, 2, 2));

        var departments = await sender.Send(new GetDepartmentsQuery());

        var department = departments.Should().ContainSingle().Subject;
        department.Code.Should().Be("CSE", "codes are stored the way staff type them");
        department.HeadTeacherName.Should().Be("Prof HOD-CSE");
        department.Programmes.Should().HaveCount(2);
        department.Programmes.Should().Contain(p =>
            p.Code == "BTCSE" && p.DurationYears == 4 && p.TermsPerYear == 2);
    }

    [Fact]
    public async Task A_cohort_belongs_to_a_programme_and_is_counted_against_it()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var deptId = await sender.Send(new CreateDepartmentCommand("Commerce", "COM", null));
        var programmeId = await sender.Send(new CreateProgrammeCommand(
            deptId, "B.Com General", "BCOM", ProgrammeLevel.Undergraduate, 3, 2));

        // The cohort is an ordinary class — that reuse is the whole design.
        var semester1 = await sender.Send(new CreateClassCommand(
            "B.Com Semester 1", 1, ["A", "B"], programmeId));
        semester1.ProgrammeId.Should().Be(programmeId);
        semester1.Sections.Should().HaveCount(2);

        var departments = await sender.Send(new GetDepartmentsQuery());
        departments.Single().Programmes.Single().Cohorts.Should().Be(1);
    }

    [Fact]
    public async Task Codes_and_names_cannot_repeat_within_one_institution()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var deptId = await sender.Send(new CreateDepartmentCommand("Physics", "PHY", null));

        var sameCode = () => sender.Send(new CreateDepartmentCommand("Physical Education", "phy", null));
        await sameCode.Should().ThrowAsync<ConflictException>().WithMessage("*code 'PHY'*");

        var sameName = () => sender.Send(new CreateDepartmentCommand("Physics", "PHYS", null));
        await sameName.Should().ThrowAsync<ConflictException>().WithMessage("*named 'Physics'*");

        await sender.Send(new CreateProgrammeCommand(
            deptId, "B.Sc Physics", "BSCPHY", ProgrammeLevel.Undergraduate, 3, 2));
        var duplicateProgramme = () => sender.Send(new CreateProgrammeCommand(
            deptId, "B.Sc Applied Physics", "bscphy", ProgrammeLevel.Undergraduate, 3, 2));
        await duplicateProgramme.Should().ThrowAsync<ConflictException>()
            .WithMessage("*BSCPHY*already exists*");
    }

    [Fact]
    public async Task A_department_cannot_close_while_it_still_runs_a_programme()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var deptId = await sender.Send(new CreateDepartmentCommand("Chemistry", "CHEM", null));
        var programmeId = await sender.Send(new CreateProgrammeCommand(
            deptId, "B.Sc Chemistry", "BSCCHM", ProgrammeLevel.Undergraduate, 3, 2));

        var closeEarly = () => sender.Send(new UpdateDepartmentCommand(
            deptId, "Chemistry", "CHEM", null, IsActive: false));
        await closeEarly.Should().ThrowAsync<ConflictException>()
            .WithMessage("*still runs active programmes*");

        // Close the programme first, and the department will follow.
        await sender.Send(new UpdateProgrammeCommand(
            programmeId, deptId, "B.Sc Chemistry", "BSCCHM",
            ProgrammeLevel.Undergraduate, 3, 2, IsActive: false));
        await sender.Send(new UpdateDepartmentCommand(
            deptId, "Chemistry", "CHEM", null, IsActive: false));

        (await sender.Send(new GetDepartmentsQuery())).Should().BeEmpty();
        (await sender.Send(new GetDepartmentsQuery(IncludeClosed: true)))
            .Should().ContainSingle(d => !d.IsActive);
    }

    [Fact]
    public async Task A_closed_programme_takes_no_new_cohorts()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var deptId = await sender.Send(new CreateDepartmentCommand("History", "HIST", null));
        var programmeId = await sender.Send(new CreateProgrammeCommand(
            deptId, "B.A History", "BAHIST", ProgrammeLevel.Undergraduate, 3, 2));
        await sender.Send(new UpdateProgrammeCommand(
            programmeId, deptId, "B.A History", "BAHIST",
            ProgrammeLevel.Undergraduate, 3, 2, IsActive: false));

        var newCohort = () => sender.Send(new CreateClassCommand(
            "B.A History Semester 1", 1, ["A"], programmeId));
        await newCohort.Should().ThrowAsync<ConflictException>().WithMessage("*closed*");
    }

    [Fact]
    public async Task A_school_that_never_creates_a_department_is_unaffected()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // No programme id: exactly what a school posts, and it must keep working.
        var grade5 = await sender.Send(new CreateClassCommand("Grade 5", 5, ["A"]));

        grade5.ProgrammeId.Should().BeNull();
        (await sender.Send(new GetDepartmentsQuery())).Should().BeEmpty();
    }

    [Fact]
    public async Task A_head_of_department_has_to_be_somebody_on_the_staff_list()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var ghost = () => sender.Send(new CreateDepartmentCommand(
            "Mathematics", "MATH", Guid.NewGuid()));
        await ghost.Should().ThrowAsync<NotFoundException>();
    }
}
