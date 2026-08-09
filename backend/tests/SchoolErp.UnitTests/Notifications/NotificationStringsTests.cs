using FluentAssertions;
using SchoolErp.Shared.Localization;

namespace SchoolErp.UnitTests.Notifications;

/// <summary>
/// Guardian-facing text is the one place a missing translation reaches a parent
/// directly, so coverage is compiler-adjacent: enforced on every build.
/// </summary>
public sealed class NotificationStringsTests
{
    private static readonly string[] AllTemplates =
    [
        NotificationTemplates.Absence,
        NotificationTemplates.ResultsPublished,
        NotificationTemplates.PaymentReceived,
        NotificationTemplates.BusBoarded,
        NotificationTemplates.BusDropped,
        NotificationTemplates.GatePass,
        NotificationTemplates.FeeReminder,
    ];

    [Fact]
    public void Telugu_covers_every_english_key_and_nothing_more()
    {
        NotificationStrings.Te.Keys.Should().BeEquivalentTo(NotificationStrings.En.Keys);
        NotificationStrings.Te.Values.Should().NotContain(string.Empty);
        NotificationStrings.En.Values.Should().NotContain(string.Empty);
    }

    [Fact]
    public void Every_template_has_a_title_and_a_body()
    {
        foreach (var template in AllTemplates)
        {
            NotificationStrings.En.Should().ContainKey($"{template}.title");
            NotificationStrings.En.Should().ContainKey($"{template}.body");
        }
    }

    [Fact]
    public void Telugu_and_english_renders_differ_for_every_template()
    {
        object?[] args = [1, 2, 3, 4, 5];
        foreach (var template in AllTemplates)
        {
            var (enTitle, enBody) = NotificationStrings.RenderMessage(
                NotificationLanguages.English, template, args);
            var (teTitle, teBody) = NotificationStrings.RenderMessage(
                NotificationLanguages.Telugu, template, args);

            teTitle.Should().NotBe(enTitle, "{0} title must be translated", template);
            teBody.Should().NotBe(enBody, "{0} body must be translated", template);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("kn-IN")]
    public void Unsupported_languages_fall_back_to_english(string? language)
    {
        NotificationStrings.Render(language, "notify.absence.title")
            .Should().Be(NotificationStrings.En["notify.absence.title"]);
    }

    [Theory]
    [InlineData("te")]
    [InlineData("TE")]
    [InlineData("te-IN")]
    public void Telugu_is_recognised_however_the_client_spells_it(string language)
    {
        NotificationLanguages.Normalize(language).Should().Be(NotificationLanguages.Telugu);
        NotificationStrings.Render(language, "notify.absence.title")
            .Should().Be(NotificationStrings.Te["notify.absence.title"]);
    }

    [Fact]
    public void An_unknown_template_key_is_a_programming_error()
    {
        var act = () => NotificationStrings.Render("en", "notify.doesNotExist.body");
        act.Should().Throw<ArgumentException>();
    }
}
