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
using SchoolErp.Application.Exams.Queries;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Staff;
using SchoolErp.Domain.Students;
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
    public async Task Admitting_into_a_cohort_records_the_programme_on_the_enrollment()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateAcademicYearCommand(
            "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
        var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

        var deptId = await sender.Send(new CreateDepartmentCommand("Economics", "ECO", null));
        var programmeId = await sender.Send(new CreateProgrammeCommand(
            deptId, "B.A Economics", "BAECO", ProgrammeLevel.Undergraduate, 3, 2));
        var cohort = await sender.Send(new CreateClassCommand(
            "B.A Economics Semester 1", 1, ["A"], programmeId));

        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Meera", "Nair", new DateOnly(2007, 4, 2), Gender.Female,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), yearId, cohort.Id, cohort.Sections.Single().Id, 1,
            [new GuardianInput("Suresh", "Nair", GuardianRelation.Father, "+919700000200", null, null, true)]));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enrollment = await db.Enrollments.SingleAsync(e => e.StudentId == studentId);
        enrollment.ProgrammeId.Should().Be(programmeId,
            "the programme is stamped from the cohort, not asked for separately");

        // And it is what the departments screen counts heads with.
        var programme = (await sender.Send(new GetDepartmentsQuery()))
            .Single().Programmes.Single();
        programme.Students.Should().Be(1);
        programme.Cohorts.Should().Be(1);
    }

    [Fact]
    public async Task Promotion_records_the_programme_the_student_moved_into()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateAcademicYearCommand(
            "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
        await sender.Send(new CreateAcademicYearCommand(
            "2027-28", new DateOnly(2027, 6, 1), new DateOnly(2028, 4, 30), MakeCurrent: false));
        var years = await sender.Send(new GetAcademicYearsQuery());
        var fromYear = years.Single(y => y.Name == "2026-27");
        var toYear = years.Single(y => y.Name == "2027-28");

        var deptId = await sender.Send(new CreateDepartmentCommand("Design", "DSGN", null));
        var diploma = await sender.Send(new CreateProgrammeCommand(
            deptId, "Diploma in Design", "DIPD", ProgrammeLevel.Diploma, 2, 2));
        var degree = await sender.Send(new CreateProgrammeCommand(
            deptId, "B.Des Design", "BDES", ProgrammeLevel.Undergraduate, 4, 2));

        var diplomaCohort = await sender.Send(new CreateClassCommand(
            "Diploma Semester 1", 1, ["A"], diploma));
        var degreeCohort = await sender.Send(new CreateClassCommand(
            "B.Des Semester 1", 2, ["A"], degree));

        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Kiran", "Rao", new DateOnly(2007, 9, 9), Gender.Male,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), fromYear.Id, diplomaCohort.Id,
            diplomaCohort.Sections.Single().Id, 1,
            [new GuardianInput("Lata", "Rao", GuardianRelation.Mother, "+919700000201", null, null, true)]));

        // A lateral move into a different programme at promotion: the new
        // enrollment must say where they actually ended up.
        await sender.Send(new PromoteClassCommand(
            fromYear.Id, diplomaCohort.Id, diplomaCohort.Sections.Single().Id,
            toYear.Id, degreeCohort.Id, degreeCohort.Sections.Single().Id, []));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enrollments = await db.Enrollments
            .Where(e => e.StudentId == studentId)
            .ToListAsync();

        enrollments.Should().HaveCount(2);
        enrollments.Single(e => e.AcademicYearId == fromYear.Id).ProgrammeId.Should().Be(diploma);
        enrollments.Single(e => e.AcademicYearId == toYear.Id).ProgrammeId.Should().Be(degree,
            "the closed placement keeps its history and the new one records the move");
    }

    [Fact]
    public async Task A_semester_advances_inside_one_academic_year()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // One Indian academic year holding an odd and an even semester.
        await sender.Send(new CreateAcademicYearCommand(
            "2026-27", new DateOnly(2026, 7, 1), new DateOnly(2027, 5, 31), MakeCurrent: true));
        var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

        var deptId = await sender.Send(new CreateDepartmentCommand("Statistics", "STAT", null));
        var programmeId = await sender.Send(new CreateProgrammeCommand(
            deptId, "B.Sc Statistics", "BSCSTAT", ProgrammeLevel.Undergraduate, 3, 2));

        var sem1 = await sender.Send(new CreateClassCommand("B.Sc Stat Semester 1", 1, ["A"], programmeId));
        var sem2 = await sender.Send(new CreateClassCommand("B.Sc Stat Semester 2", 2, ["A"], programmeId));
        var sem3 = await sender.Send(new CreateClassCommand("B.Sc Stat Semester 3", 3, ["A"], programmeId));

        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Anil", "Kumar", new DateOnly(2007, 3, 3), Gender.Male,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 7, 5), yearId, sem1.Id, sem1.Sections.Single().Id, 1,
            [new GuardianInput("Radha", "Kumar", GuardianRelation.Mother, "+919700000300", null, null, true)]));

        // Semester 1 → 2, same year. This is the move that used to be refused.
        var toSem2 = await sender.Send(new PromoteClassCommand(
            yearId, sem1.Id, sem1.Sections.Single().Id,
            yearId, sem2.Id, sem2.Sections.Single().Id, []));
        toSem2.Promoted.Should().Be(1);
        toSem2.AlreadyEnrolled.Should().Be(0,
            "the row being moved out of is not a competing placement");

        // ...and again into Semester 3. The closed Semester 1 row lives in the
        // same year and must not read as "already placed".
        var toSem3 = await sender.Send(new PromoteClassCommand(
            yearId, sem2.Id, sem2.Sections.Single().Id,
            yearId, sem3.Id, sem3.Sections.Single().Id, []));
        toSem3.Promoted.Should().Be(1);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enrollments = await db.Enrollments
            .Where(e => e.StudentId == studentId)
            .ToListAsync();

        enrollments.Should().HaveCount(3, "each semester is its own placement, kept as history");
        enrollments.Count(e => e.Status == EnrollmentStatus.Active)
            .Should().Be(1, "a student is only ever in one semester at a time");
        enrollments.Single(e => e.Status == EnrollmentStatus.Active)
            .SchoolClassId.Should().Be(sem3.Id);
        enrollments.Should().OnlyContain(e => e.ProgrammeId == programmeId);
    }

    [Fact]
    public async Task Re_running_a_semester_advance_changes_nothing()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateAcademicYearCommand(
            "2026-27", new DateOnly(2026, 7, 1), new DateOnly(2027, 5, 31), MakeCurrent: true));
        var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

        var deptId = await sender.Send(new CreateDepartmentCommand("Botany", "BOT", null));
        var programmeId = await sender.Send(new CreateProgrammeCommand(
            deptId, "B.Sc Botany", "BSCBOT", ProgrammeLevel.Undergraduate, 3, 2));
        var sem1 = await sender.Send(new CreateClassCommand("Botany Semester 1", 1, ["A"], programmeId));
        var sem2 = await sender.Send(new CreateClassCommand("Botany Semester 2", 2, ["A"], programmeId));

        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Sneha", "Pillai", new DateOnly(2007, 8, 8), Gender.Female,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 7, 5), yearId, sem1.Id, sem1.Sections.Single().Id, 1,
            [new GuardianInput("Anu", "Pillai", GuardianRelation.Mother, "+919700000301", null, null, true)]));

        await sender.Send(new PromoteClassCommand(
            yearId, sem1.Id, sem1.Sections.Single().Id,
            yearId, sem2.Id, sem2.Sections.Single().Id, []));
        var again = await sender.Send(new PromoteClassCommand(
            yearId, sem1.Id, sem1.Sections.Single().Id,
            yearId, sem2.Id, sem2.Sections.Single().Id, []));

        again.Promoted.Should().Be(0);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Enrollments.CountAsync(e => e.StudentId == studentId)).Should().Be(2);
    }

    [Fact]
    public async Task Promotion_that_moves_nobody_anywhere_is_refused()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateAcademicYearCommand(
            "2026-27", new DateOnly(2026, 7, 1), new DateOnly(2027, 5, 31), MakeCurrent: true));
        var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
        var cohort = await sender.Send(new CreateClassCommand("Semester 1", 1, ["A"]));
        var sectionId = cohort.Sections.Single().Id;

        var noop = () => sender.Send(new PromoteClassCommand(
            yearId, cohort.Id, sectionId, yearId, cohort.Id, sectionId, []));
        await noop.Should().ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*must move students somewhere*");
    }

    [Fact]
    public async Task A_published_semester_produces_an_sgpa_and_a_cgpa()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateAcademicYearCommand(
            "2026-27", new DateOnly(2026, 7, 1), new DateOnly(2027, 5, 31), MakeCurrent: true));
        var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

        var deptId = await sender.Send(new CreateDepartmentCommand("Computer Applications", "MCA", null));
        var programmeId = await sender.Send(new CreateProgrammeCommand(
            deptId, "Master of Computer Applications", "MCAP",
            ProgrammeLevel.Postgraduate, 2, 2));
        var sem1 = await sender.Send(new CreateClassCommand("MCA Semester 1", 1, ["A"], programmeId));
        var sectionId = sem1.Sections.Single().Id;

        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Farhan", "Sheikh", new DateOnly(2004, 2, 2), Gender.Male,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 7, 5), yearId, sem1.Id, sectionId, 1,
            [new GuardianInput("Nasreen", "Sheikh", GuardianRelation.Mother, "+919700000400", null, null, true)]));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enrollmentId = await db.Enrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.Id)
            .SingleAsync();

        var algorithms = await sender.Send(new CreateSubjectCommand("Algorithms", "ALG"));
        var databases = await sender.Send(new CreateSubjectCommand("Databases", "DBMS"));

        var examId = await sender.Send(new CreateExamCommand(
            "Semester 1 End", yearId, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 30)));

        // 4 credits at 75% → A (8), 2 credits at 42% → P (4).
        // (4×8 + 2×4) / 6 = 6.67 — credit-weighted, not the 6.00 plain average.
        var algoPaper = await sender.Send(new ScheduleExamSubjectCommand(
            examId, sem1.Id, algorithms.Id, new DateOnly(2026, 11, 21), 100m, 40m, Credits: 4));
        var dbPaper = await sender.Send(new ScheduleExamSubjectCommand(
            examId, sem1.Id, databases.Id, new DateOnly(2026, 11, 24), 100m, 40m, Credits: 2));

        await sender.Send(new EnterMarksCommand(algoPaper, [new MarkInput(enrollmentId, 75m, false)]));
        await sender.Send(new EnterMarksCommand(dbPaper, [new MarkInput(enrollmentId, 42m, false)]));

        // Nothing counts until results are published.
        var draft = await sender.Send(new GetStudentGradeSheetQuery(studentId));
        draft.Semesters.Should().BeEmpty();
        draft.Cgpa.Should().BeNull();
        draft.Unavailable.Should().Be("No published results yet.");

        await sender.Send(new PublishExamCommand(examId));

        var sheet = await sender.Send(new GetStudentGradeSheetQuery(studentId));

        sheet.ProgrammeName.Should().Be("Master of Computer Applications");
        sheet.Unavailable.Should().BeNull();

        var semester = sheet.Semesters.Should().ContainSingle().Subject;
        semester.Sgpa.Should().Be(6.67m);
        semester.CreditsAttempted.Should().Be(6);
        semester.CreditsEarned.Should().Be(6, "both papers were passed");
        semester.Papers.Should().Contain(p => p.SubjectName == "Algorithms" && p.Grade == "A");
        semester.Papers.Should().Contain(p => p.SubjectName == "Databases" && p.Grade == "P");

        // One semester in, the CGPA is that semester.
        sheet.Cgpa.Should().Be(6.67m);
        sheet.CreditsEarned.Should().Be(6);
    }

    [Fact]
    public async Task A_school_exam_reports_no_gpa_rather_than_zero()
    {
        var tenantId = await _fixture.NewCollegeAsync();

        await using var scope = _fixture.CreateScope(tenantId);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new CreateAcademicYearCommand(
            "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
        var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

        // No programme, no credits — exactly how a school schedules a paper.
        var grade5 = await sender.Send(new CreateClassCommand("Grade 5", 5, ["A"]));
        var sectionId = grade5.Sections.Single().Id;

        var studentId = await sender.Send(new AdmitStudentCommand(
            null, "Ishaan", "Verma", new DateOnly(2015, 6, 6), Gender.Male,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), yearId, grade5.Id, sectionId, 1,
            [new GuardianInput("Kavita", "Verma", GuardianRelation.Mother, "+919700000401", null, null, true)]));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enrollmentId = await db.Enrollments
            .Where(e => e.StudentId == studentId).Select(e => e.Id).SingleAsync();

        var maths = await sender.Send(new CreateSubjectCommand("Mathematics", "MATH"));
        var examId = await sender.Send(new CreateExamCommand(
            "Mid-Term 1", yearId, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)));
        var paper = await sender.Send(new ScheduleExamSubjectCommand(
            examId, grade5.Id, maths.Id, new DateOnly(2026, 9, 2), 100m, 33m));
        await sender.Send(new EnterMarksCommand(paper, [new MarkInput(enrollmentId, 88m, false)]));
        await sender.Send(new PublishExamCommand(examId));

        var sheet = await sender.Send(new GetStudentGradeSheetQuery(studentId));

        // The result exists; the GPA does not, and says why rather than
        // reporting 0.00 as though the child had failed.
        sheet.Semesters.Should().ContainSingle();
        sheet.Semesters.Single().Sgpa.Should().BeNull();
        sheet.Cgpa.Should().BeNull();
        sheet.Unavailable.Should().Contain("No paper carries credits");
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
