using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Attendance;
using SchoolErp.Application.Attendance.Commands;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Notifications;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Notifications;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Auth;
using SchoolErp.IntegrationTests.Tenancy;
using SchoolErp.Shared.Localization;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Notifications;

/// <summary>
/// One section with two children whose guardians read different languages —
/// the shape every real Indian school has.
/// </summary>
public sealed class NotificationLocalizationFixture : IAsyncLifetime
{
    public const string EnglishGuardianPhone = "+919650000001";

    public const string TeluguGuardianPhone = "+919650000002";

    public const string TeluguGuardianDevice = "ExponentPushToken[telugu-device]";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_notify_l10n_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public const string SchoolName = "Localization Test School";

    public Guid SectionId { get; private set; }

    /// <summary>Ravi — guardian reads English.</summary>
    public Guid EnglishEnrollment { get; private set; }

    /// <summary>Sita — guardian reads Telugu.</summary>
    public Guid TeluguEnrollment { get; private set; }

    public RecordingSmsSender SmsSender { get; } = new();

    public ToggleWhatsAppSender WhatsAppSender { get; } = new();

    public RecordingPushSender PushSender { get; } = new();

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
        services.AddSingleton<ISmsSender>(SmsSender);
        services.AddSingleton<IWhatsAppSender>(WhatsAppSender);
        services.AddSingleton<SchoolErp.Application.Notifications.IPushSender>(PushSender);
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "L10N01",
                Name = SchoolName,
                Subdomain = "l10n",
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
            SectionId = schoolClass.Sections.Single().Id;
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;

            var english = await AdmitAsync(
                sender, yearId, schoolClass.Id, "Ravi", EnglishGuardianPhone, "en", 1);
            var telugu = await AdmitAsync(
                sender, yearId, schoolClass.Id, "Sita", TeluguGuardianPhone, "te", 2);

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            EnglishEnrollment = await db.Enrollments
                .Where(e => e.StudentId == english).Select(e => e.Id).SingleAsync();
            TeluguEnrollment = await db.Enrollments
                .Where(e => e.StudentId == telugu).Select(e => e.Id).SingleAsync();

            // The Telugu-reading guardian also has the app installed, so push
            // must come out in Telugu too — not just the SMS.
            db.PushTokens.Add(new PushToken
            {
                UserId = Guid.NewGuid(),
                Phone = TeluguGuardianPhone,
                Token = TeluguGuardianDevice,
                Platform = "android",
            });
            await db.SaveChangesAsync();
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

    /// <summary>Marks a daily absence and drains the outbox, as production does.</summary>
    public async Task MarkAbsentAndDispatchAsync(Guid enrollmentId, DateOnly date)
    {
        await using (var scope = CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new MarkAttendanceCommand(SectionId, date,
                    [new AttendanceEntry(enrollmentId, AttendanceStatus.Absent, null)]));
        }

        // The dispatcher runs without a tenant bound, exactly as the job does.
        await using var dispatcherScope = _provider.CreateAsyncScope();
        await dispatcherScope.ServiceProvider
            .GetRequiredService<OutboxProcessor>().ProcessPendingAsync();
    }

    public async Task SetWhatsAppEnabledAsync(bool enabled)
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == TenantId);
        tenant.WhatsAppEnabled = enabled;
        await db.SaveChangesAsync();
    }

    public async Task<string> LanguageOfAsync(string phone)
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Guardians.AsNoTracking()
            .Where(g => g.Phone == phone)
            .Select(g => g.PreferredLanguage)
            .FirstAsync();
    }

    private async Task<Guid> AdmitAsync(
        ISender sender, Guid yearId, Guid classId,
        string firstName, string guardianPhone, string language, int roll) =>
        await sender.Send(new AdmitStudentCommand(
            null, firstName, "Kumar", new DateOnly(2016, 3, 12), Gender.Female,
            null, null, null, null, null, null, null, null,
            new DateOnly(2026, 6, 5), yearId, classId, SectionId, roll,
            [new GuardianInput(
                "Guardian", "Kumar", GuardianRelation.Mother, guardianPhone,
                null, null, IsPrimary: true, PreferredLanguage: language)]));
}

/// <summary>
/// The point of the feature: a Telugu-reading parent is written to in Telugu,
/// on every channel, without changing what the English-reading parent receives.
/// </summary>
public sealed class NotificationLocalizationTests
    : IClassFixture<NotificationLocalizationFixture>
{
    private readonly NotificationLocalizationFixture _fixture;

    public NotificationLocalizationTests(NotificationLocalizationFixture fixture) =>
        _fixture = fixture;

    private static string ExpectedAbsenceBody(string language, string studentName, DateOnly date) =>
        NotificationStrings.Render(
            language, $"{NotificationTemplates.Absence}.body",
            studentName, date, NotificationLocalizationFixture.SchoolName);

    [Fact]
    public async Task A_telugu_guardian_gets_telugu_sms_and_an_english_one_still_gets_english()
    {
        var date = new DateOnly(2026, 7, 6);

        await _fixture.MarkAbsentAndDispatchAsync(_fixture.EnglishEnrollment, date);
        await _fixture.MarkAbsentAndDispatchAsync(_fixture.TeluguEnrollment, date);

        // Tests share the fixture's recorder — always read the LATEST message
        // to a phone rather than assuming this test ran first.
        var english = _fixture.SmsSender.Sent
            .Last(s => s.Phone == NotificationLocalizationFixture.EnglishGuardianPhone);
        var telugu = _fixture.SmsSender.Sent
            .Last(s => s.Phone == NotificationLocalizationFixture.TeluguGuardianPhone);

        english.Message.Should().Be(ExpectedAbsenceBody("en", "Ravi", date));
        english.Message.Should().Contain("was marked absent");

        telugu.Message.Should().Be(ExpectedAbsenceBody("te", "Sita", date));
        telugu.Message.Should().Contain("గైర్హాజరు",
            "the Telugu guardian must read the alert in Telugu");
        // The child's name is school-entered data and stays exactly as entered.
        telugu.Message.Should().Contain("Sita").And.Contain(NotificationLocalizationFixture.SchoolName);
    }

    [Fact]
    public async Task Push_to_the_same_guardian_is_localized_title_and_body()
    {
        await _fixture.MarkAbsentAndDispatchAsync(
            _fixture.TeluguEnrollment, new DateOnly(2026, 7, 7));

        _fixture.PushSender.Sent.Should().Contain(p =>
            p.Token == NotificationLocalizationFixture.TeluguGuardianDevice &&
            p.Title == NotificationStrings.Te["notify.absence.title"] &&
            p.Body.Contains("గైర్హాజరు"));
    }

    [Fact]
    public async Task Whatsapp_carries_the_same_localized_text_as_sms_would()
    {
        await _fixture.SetWhatsAppEnabledAsync(true);
        try
        {
            var date = new DateOnly(2026, 7, 8);
            await _fixture.MarkAbsentAndDispatchAsync(_fixture.TeluguEnrollment, date);

            _fixture.WhatsAppSender.Sent.Should().Contain(s =>
                s.Phone == NotificationLocalizationFixture.TeluguGuardianPhone &&
                s.Message == ExpectedAbsenceBody("te", "Sita", date));
        }
        finally
        {
            await _fixture.SetWhatsAppEnabledAsync(false);
        }
    }

    [Fact]
    public async Task The_parent_app_toggle_switches_the_language_of_later_alerts()
    {
        try
        {
            // Ravi's guardian flips the app to Telugu; nothing else changes.
            await SetLanguageAsync(NotificationLocalizationFixture.EnglishGuardianPhone, "te");

            (await _fixture.LanguageOfAsync(
                NotificationLocalizationFixture.EnglishGuardianPhone)).Should().Be("te");

            var date = new DateOnly(2026, 7, 9);
            await _fixture.MarkAbsentAndDispatchAsync(_fixture.EnglishEnrollment, date);

            _fixture.SmsSender.Sent.Last(s =>
                    s.Phone == NotificationLocalizationFixture.EnglishGuardianPhone)
                .Message.Should().Be(ExpectedAbsenceBody("te", "Ravi", date));
        }
        finally
        {
            // Restore, so this test does not have to run last.
            await SetLanguageAsync(NotificationLocalizationFixture.EnglishGuardianPhone, "en");
        }
    }

    private async Task SetLanguageAsync(string phone, string language)
    {
        await using var scope = _fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SetMyNotificationLanguageCommand(phone, language));
    }

    [Fact]
    public async Task An_unsupported_language_is_refused_rather_than_silently_stored()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new SetMyNotificationLanguageCommand(
            NotificationLocalizationFixture.TeluguGuardianPhone, "fr"));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        (await _fixture.LanguageOfAsync(NotificationLocalizationFixture.TeluguGuardianPhone))
            .Should().Be("te", "a rejected change must leave the preference alone");
    }
}
