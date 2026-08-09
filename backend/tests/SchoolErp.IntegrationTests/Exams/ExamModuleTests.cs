using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Exams;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Exams.Queries;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Exams;

/// <summary>
/// One school, one class ("Grade 7 A") with two students, one exam with two
/// papers — enough to exercise the whole exam lifecycle including rank.
/// </summary>
public sealed class ExamModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_exam_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid YearId { get; private set; }

    public Guid ClassId { get; private set; }

    public Guid SectionId { get; private set; }

    public Guid StudentTop { get; private set; }

    public Guid StudentSecond { get; private set; }

    public Guid EnrollmentTop { get; private set; }

    public Guid EnrollmentSecond { get; private set; }

    public Guid MathSubjectId { get; private set; }

    public Guid ScienceSubjectId { get; private set; }

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
                Code = "EXAM01",
                Name = "Exam Test School",
                Subdomain = "examtest",
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
            YearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

            var grade7 = await sender.Send(new CreateClassCommand("Grade 7", 7, ["A"]));
            ClassId = grade7.Id;
            SectionId = grade7.Sections.Single().Id;

            MathSubjectId = (await sender.Send(new CreateSubjectCommand("Mathematics", "MATH"))).Id;
            ScienceSubjectId = (await sender.Send(new CreateSubjectCommand("Science", "SCI"))).Id;

            StudentTop = await AdmitAsync(sender, "Topper", "+919600000001", 1);
            StudentSecond = await AdmitAsync(sender, "Runner", "+919600000002", 2);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnrollmentTop = await db.Enrollments.Where(e => e.StudentId == StudentTop)
                .Select(e => e.Id).SingleAsync();
            EnrollmentSecond = await db.Enrollments.Where(e => e.StudentId == StudentSecond)
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

    private async Task<Guid> AdmitAsync(ISender sender, string firstName, string phone, int roll) =>
        await sender.Send(new AdmitStudentCommand(
            null, firstName, "Singh", new DateOnly(2013, 3, 10), Gender.Female,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), YearId, ClassId, SectionId, roll,
            [new GuardianInput("Parent", "Singh", GuardianRelation.Mother, phone, null, null, true)]));
}

/// <summary>The exam lifecycle through the full pipeline.</summary>
public sealed class ExamModuleTests : IClassFixture<ExamModuleFixture>
{
    private readonly ExamModuleFixture _fixture;

    public ExamModuleTests(ExamModuleFixture fixture) => _fixture = fixture;

    private async Task<(Guid ExamId, Guid MathPaper, Guid SciencePaper)> SetUpExamAsync(
        ISender sender, string name)
    {
        var examId = await sender.Send(new CreateExamCommand(
            name, _fixture.YearId, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 20)));
        var math = await sender.Send(new ScheduleExamSubjectCommand(
            examId, _fixture.ClassId, _fixture.MathSubjectId, new DateOnly(2026, 9, 10), 100, 33));
        var science = await sender.Send(new ScheduleExamSubjectCommand(
            examId, _fixture.ClassId, _fixture.ScienceSubjectId, new DateOnly(2026, 9, 12), 50, 17));
        return (examId, math, science);
    }

    [Fact]
    public async Task Full_lifecycle_marks_publish_result_and_rank()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (examId, mathPaper, sciencePaper) = await SetUpExamAsync(sender, "Mid-Term 1");

        // Topper: 92/100 + 46/50 = 138/150 → 92% A1. Runner: 60 + 25 = 85/150.
        await sender.Send(new EnterMarksCommand(mathPaper,
        [
            new MarkInput(_fixture.EnrollmentTop, 92, false),
            new MarkInput(_fixture.EnrollmentSecond, 60, false),
        ]));
        await sender.Send(new EnterMarksCommand(sciencePaper,
        [
            new MarkInput(_fixture.EnrollmentTop, 46, false),
            new MarkInput(_fixture.EnrollmentSecond, 25, false),
        ]));

        await sender.Send(new PublishExamCommand(examId));

        var top = await sender.Send(new GetStudentResultQuery(_fixture.StudentTop, examId));
        top.TotalObtained.Should().Be(138);
        top.TotalMax.Should().Be(150);
        top.Percent.Should().Be(92);
        top.OverallGrade.Should().Be("A1");
        top.SectionRank.Should().Be(1);
        top.SectionSize.Should().Be(2);
        top.Lines.Should().HaveCount(2).And.OnlyContain(l => l.Passed);

        var second = await sender.Send(new GetStudentResultQuery(_fixture.StudentSecond, examId));
        second.SectionRank.Should().Be(2);

        // Publication queued one SMS per student with marks. (Payload is jsonb,
        // which has no LIKE operator — filter client-side.)
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await db.OutboxMessages
            .Where(m => m.TenantId == _fixture.TenantId)
            .ToListAsync();
        outbox.Count(m => m.Payload.Contains("Mid-Term 1")).Should().Be(2);
    }

    [Fact]
    public async Task Report_card_renders_a_pdf_and_hides_drafts_from_parents()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (examId, mathPaper, sciencePaper) = await SetUpExamAsync(sender, "Report Card Exam");

        await sender.Send(new EnterMarksCommand(mathPaper,
            [new MarkInput(_fixture.EnrollmentTop, 88, false)]));
        await sender.Send(new EnterMarksCommand(sciencePaper,
            [new MarkInput(_fixture.EnrollmentTop, 40, false)]));

        // Parent-facing rendering must refuse drafts…
        var draft = () => sender.Send(
            new GetReportCardPdfQuery(_fixture.StudentTop, examId, PublishedOnly: true));
        await draft.Should().ThrowAsync<SchoolErp.Application.Common.Exceptions.NotFoundException>();

        // …while staff can proof them.
        var proof = await sender.Send(new GetReportCardPdfQuery(_fixture.StudentTop, examId));
        System.Text.Encoding.ASCII.GetString(proof, 0, 5).Should().Be("%PDF-");
        proof.Length.Should().BeGreaterThan(2000, "a rendered A4 report card is not a stub");

        // After publication the parent-facing render succeeds too.
        await sender.Send(new PublishExamCommand(examId));
        var published = await sender.Send(
            new GetReportCardPdfQuery(_fixture.StudentTop, examId, PublishedOnly: true));
        System.Text.Encoding.ASCII.GetString(published, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Report_card_settings_round_trip_and_every_template_renders()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (examId, mathPaper, sciencePaper) = await SetUpExamAsync(sender, "Template Exam");
        await sender.Send(new EnterMarksCommand(mathPaper,
            [new MarkInput(_fixture.EnrollmentTop, 71, false)]));
        await sender.Send(new EnterMarksCommand(sciencePaper,
            [new MarkInput(_fixture.EnrollmentTop, 33, false)]));

        var defaults = await sender.Send(new GetReportCardSettingsQuery());
        defaults.Template.Should().Be(ReportCardTemplate.MarksAndGrades);
        defaults.Signatories.Should().BeEquivalentTo(ReportCardDefaults.Signatories);

        try
        {
            // Every template must produce a real PDF, not just the default one.
            foreach (var template in Enum.GetValues<ReportCardTemplate>())
            {
                var saved = await sender.Send(new UpdateReportCardSettingsCommand(
                    template, ShowAttendance: true, ShowRemarks: true,
                    ["Class teacher", "Principal"]));
                saved.Template.Should().Be(template);
                saved.Signatories.Should().HaveCount(2);

                var pdf = await sender.Send(
                    new GetReportCardPdfQuery(_fixture.StudentTop, examId));
                System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
                pdf.Length.Should().BeGreaterThan(2000, $"{template} must render a full page");
            }

            // Blank signature lines fall back to the defaults rather than
            // printing an empty row.
            var blanked = await sender.Send(new UpdateReportCardSettingsCommand(
                ReportCardTemplate.MarksOnly, ShowAttendance: false, ShowRemarks: false, []));
            blanked.Signatories.Should().BeEquivalentTo(ReportCardDefaults.Signatories);

            var tooMany = () => sender.Send(new UpdateReportCardSettingsCommand(
                ReportCardTemplate.MarksOnly, false, false,
                ["One", "Two", "Three", "Four", "Five"]));
            await tooMany.Should().ThrowAsync<FluentValidation.ValidationException>();
        }
        finally
        {
            // Shared fixture: put the school's layout back.
            await sender.Send(new UpdateReportCardSettingsCommand(
                ReportCardTemplate.MarksAndGrades, false, false,
                ReportCardDefaults.Signatories));
        }
    }

    [Fact]
    public async Task Marks_above_paper_maximum_are_rejected()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (_, mathPaper, _) = await SetUpExamAsync(sender, "Range Check");

        var act = () => sender.Send(new EnterMarksCommand(mathPaper,
            [new MarkInput(_fixture.EnrollmentTop, 101, false)]));
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*exceed*");
    }

    [Fact]
    public async Task Published_exams_freeze_marks_and_absent_students_grade_AB()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (examId, mathPaper, sciencePaper) = await SetUpExamAsync(sender, "Freeze Check");

        await sender.Send(new EnterMarksCommand(mathPaper,
        [
            new MarkInput(_fixture.EnrollmentTop, 80, false),
            new MarkInput(_fixture.EnrollmentSecond, null, true), // absent
        ]));
        await sender.Send(new EnterMarksCommand(sciencePaper,
            [new MarkInput(_fixture.EnrollmentTop, 40, false)]));
        await sender.Send(new PublishExamCommand(examId));

        // Frozen after publish.
        var act = () => sender.Send(new EnterMarksCommand(mathPaper,
            [new MarkInput(_fixture.EnrollmentTop, 85, false)]));
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*frozen*");

        // Absent line grades as AB and does not pass.
        var runner = await sender.Send(new GetStudentResultQuery(_fixture.StudentSecond, examId));
        var mathLine = runner.Lines.Single(l => l.SubjectName == "Mathematics");
        mathLine.IsAbsent.Should().BeTrue();
        mathLine.Grade.Should().Be("AB");
        mathLine.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Publishing_without_any_marks_is_rejected()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (examId, _, _) = await SetUpExamAsync(sender, "Empty Publish");

        var act = () => sender.Send(new PublishExamCommand(examId));
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*no marks*");
    }

    [Fact]
    public async Task Duplicate_paper_for_same_class_and_subject_conflicts()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var (examId, _, _) = await SetUpExamAsync(sender, "Dup Paper");

        var act = () => sender.Send(new ScheduleExamSubjectCommand(
            examId, _fixture.ClassId, _fixture.MathSubjectId, null, 100, 33));
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already scheduled*");
    }

    [Fact]
    public async Task Term_report_weights_exams_and_renders_with_remarks()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Two exams: term1 (60% weight) and term2 (40%).
        var (term1, math1, science1) = await SetUpExamAsync(sender, "TR Term 1");
        var (term2, math2, science2) = await SetUpExamAsync(sender, "TR Term 2");
        await sender.Send(new EnterMarksCommand(math1,
            [new MarkInput(_fixture.EnrollmentTop, 80, false)]));
        await sender.Send(new EnterMarksCommand(science1,
            [new MarkInput(_fixture.EnrollmentTop, 40, false)]));
        await sender.Send(new EnterMarksCommand(math2,
            [new MarkInput(_fixture.EnrollmentTop, 100, false)]));
        await sender.Send(new EnterMarksCommand(science2,
            [new MarkInput(_fixture.EnrollmentTop, 25, false)]));

        var reportId = await sender.Send(new CreateTermReportCommand(
            _fixture.YearId, "Annual Weighted", [(term1, 60m), (term2, 40m)]));

        // Weights must sum to 100.
        var badWeights = () => sender.Send(new CreateTermReportCommand(
            _fixture.YearId, "Broken", [(term1, 50m), (term2, 40m)]));
        await badWeights.Should().ThrowAsync<FluentValidation.ValidationException>();

        await sender.Send(new SetTermStudentInputCommand(
            reportId, _fixture.StudentTop,
            new Dictionary<string, string> { ["Art"] = "A", ["Sports"] = "B" },
            "Consistent and disciplined through the year."));

        // Staff render works with drafts; parent view demands published exams.
        var pdf = await sender.Send(
            new GetTermReportCardPdfQuery(reportId, _fixture.StudentTop));
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");

        var parentView = () => sender.Send(
            new GetTermReportCardPdfQuery(reportId, _fixture.StudentTop, PublishedOnly: true));
        await parentView.Should().ThrowAsync<NotFoundException>("components are drafts");

        await sender.Send(new PublishExamCommand(term1));
        await sender.Send(new PublishExamCommand(term2));
        var published = await sender.Send(
            new GetTermReportCardPdfQuery(reportId, _fixture.StudentTop, PublishedOnly: true));
        System.Text.Encoding.ASCII.GetString(published, 0, 4).Should().Be("%PDF");

        // Weighted math: 80% (60w) + 100% (40w) → 88%.
        // (Grade math is covered in unit form by the renderer inputs — here we
        // just prove listing carries components + weights.)
        var listed = (await sender.Send(new GetTermReportsQuery(_fixture.YearId)))
            .Single(t => t.Id == reportId);
        listed.Components.Should().HaveCount(2);
        listed.Components.Sum(c => c.WeightPercent).Should().Be(100);
    }
}
