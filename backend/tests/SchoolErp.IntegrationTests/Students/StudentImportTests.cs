using ClosedXML.Excel;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Students;

/// <summary>One school with a current year and one class to import into.</summary>
public sealed class StudentImportFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_import_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

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
                Code = "IMPRT1",
                Name = "Import Test School",
                Subdomain = "importtest",
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
            await sender.Send(new CreateClassCommand("Grade 3", 3, ["A", "B"]));
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

    /// <summary>Fills the real template's Students sheet with the given rows.</summary>
    public static byte[] FillTemplate(byte[] template, params string?[][] rows)
    {
        using var workbook = new XLWorkbook(new MemoryStream(template));
        var sheet = workbook.Worksheet("Students");
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                if (rows[r][c] is { } value)
                {
                    sheet.Cell(2 + r, 1 + c).Value = value;
                }
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

/// <summary>Template download + all-or-nothing Excel import.</summary>
public sealed class StudentImportTests : IClassFixture<StudentImportFixture>
{
    private readonly StudentImportFixture _fixture;

    public StudentImportTests(StudentImportFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Template_lists_the_schools_classes_and_a_clean_file_imports_everyone()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var template = await sender.Send(new GetStudentImportTemplateQuery());
        using (var workbook = new XLWorkbook(new MemoryStream(template)))
        {
            workbook.Worksheet("Students").Cell(1, 2).GetString()
                .Should().Contain("First name");
            workbook.Worksheet("Instructions").CellsUsed()
                .Any(c => c.GetString() == "Grade 3").Should().BeTrue(
                    "the template must list this school's real classes");
        }

        var filled = StudentImportFixture.FillTemplate(template,
            [null, "Riya", "Mehta", "2018-03-14", "Female", "Grade 3", "A", "1",
             null, "O+", "Hyderabad", "Telangana", "Pooja", "Mehta", "Mother",
             "+919811100001", "pooja@example.com"],
            ["ADM-X-77", "Kabir", "Mehta", "2017-11-02", "Male", "grade 3", "b", "2",
             "2026-06-15", null, null, null, "Pooja", "Mehta", "Mother",
             "+919811100001", null]);

        var result = await sender.Send(new ImportStudentsCommand(filled));

        result.TotalRows.Should().Be(2);
        result.Imported.Should().Be(2);
        result.Errors.Should().BeEmpty();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var riya = await db.Students.SingleAsync(s => s.FirstName == "Riya");
        riya.AdmissionNumber.Should().NotBeNullOrWhiteSpace("blank number auto-generates");
        var kabir = await db.Students.SingleAsync(s => s.FirstName == "Kabir");
        kabir.AdmissionNumber.Should().Be("ADM-X-77");

        // Same guardian phone on both rows → one shared guardian (siblings).
        var guardianIds = await db.StudentGuardians
            .Where(g => g.StudentId == riya.Id || g.StudentId == kabir.Id)
            .Select(g => g.GuardianId)
            .ToListAsync();
        guardianIds.Distinct().Should().HaveCount(1);

        // Case-insensitive class/section resolution placed Kabir in Grade 3 B.
        (await db.Enrollments.CountAsync(e => e.StudentId == kabir.Id))
            .Should().Be(1);
    }

    [Fact]
    public async Task A_file_with_any_bad_row_imports_nothing_and_reports_each_problem()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var template = await sender.Send(new GetStudentImportTemplateQuery());
        var filled = StudentImportFixture.FillTemplate(template,
            [null, "Neel", "Shah", "2018-01-20", "Male", "Grade 3", "A", null,
             null, null, null, null, "Amit", "Shah", "Father", "+919811100002", null],
            [null, "Zoya", null, "not-a-date", "Alien", "Grade 9", "Z", null,
             null, null, null, null, "Sana", "Sheikh", "Cousin", "12", null]);

        var before = await CountStudentsAsync(scope);
        var result = await sender.Send(new ImportStudentsCommand(filled));

        result.Imported.Should().Be(0, "one bad row rejects the whole file");
        result.Errors.Should().OnlyContain(e => e.RowNumber == 3);
        var messages = string.Join(" | ", result.Errors.Select(e => e.Message));
        messages.Should().Contain("Last name").And.Contain("not-a-date")
            .And.Contain("Alien").And.Contain("Grade 9").And.Contain("Cousin")
            .And.Contain("phone");

        (await CountStudentsAsync(scope)).Should().Be(before, "nothing may be imported");
    }

    [Fact]
    public async Task Reuploading_the_same_file_is_rejected_instead_of_duplicating_children()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var template = await sender.Send(new GetStudentImportTemplateQuery());
        var filled = StudentImportFixture.FillTemplate(template,
            [null, "Ira", "Bose", "2018-05-05", "Female", "Grade 3", "A", null,
             null, null, null, null, "Rina", "Bose", "Mother", "+919811100003", null]);

        (await sender.Send(new ImportStudentsCommand(filled))).Imported.Should().Be(1);

        var again = await sender.Send(new ImportStudentsCommand(filled));
        again.Imported.Should().Be(0);
        again.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task A_non_template_file_is_rejected_with_a_helpful_message()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new ImportStudentsCommand([1, 2, 3, 4]));
        await act.Should().ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*could not be read as an Excel workbook*");
    }

    [Fact]
    public async Task Student_list_sorts_server_side_by_whitelisted_keys()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var yearId = await db.AcademicYears.Where(y => y.IsCurrent)
            .Select(y => y.Id).SingleAsync();
        var section = await db.SchoolClasses
            .SelectMany(c => c.Sections.Select(s => new { c.Id, SectionId = s.Id }))
            .FirstAsync();
        foreach (var (first, roll) in new[] { ("Aaa", 41), ("Bbb", 42) })
        {
            await sender.Send(new SchoolErp.Application.Students.Commands.AdmitStudentCommand(
                null, first, "Zorder", new DateOnly(2018, 1, 1), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, section.Id, section.SectionId, roll,
                [new SchoolErp.Application.Students.GuardianInput(
                    "G", "Zorder", GuardianRelation.Father,
                    $"+9198111001{roll}", null, null, true)]));
        }

        var byNameDesc = await sender.Send(new SchoolErp.Application.Students.Queries.GetStudentsQuery(
            Search: "Zorder", SortBy: "name", SortDescending: true));
        byNameDesc.Items.Select(s => s.FirstName).Should().ContainInOrder("Bbb", "Aaa");

        var byRollAsc = await sender.Send(new SchoolErp.Application.Students.Queries.GetStudentsQuery(
            Search: "Zorder", SortBy: "roll"));
        byRollAsc.Items.Select(s => s.RollNumber).Should().ContainInOrder(41, 42);

        // Unknown keys fall back to name order instead of failing.
        var fallback = await sender.Send(new SchoolErp.Application.Students.Queries.GetStudentsQuery(
            Search: "Zorder", SortBy: "drop table"));
        fallback.Items.Select(s => s.FirstName).Should().ContainInOrder("Aaa", "Bbb");

        // The export honours the same filter and sort.
        var export = await sender.Send(new SchoolErp.Application.Students.Queries.ExportStudentsQuery(
            "Zorder", null, null, null, null, "name", true));
        using var workbook = new XLWorkbook(new MemoryStream(export));
        var sheet = workbook.Worksheet("Students");
        sheet.Cell(2, 2).GetString().Should().Be("Bbb");
        sheet.Cell(3, 2).GetString().Should().Be("Aaa");
    }

    private static async Task<int> CountStudentsAsync(AsyncServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Students.CountAsync();
    }
}
