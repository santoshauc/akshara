using FluentAssertions;
using SchoolErp.Shared.Localization;

namespace SchoolErp.UnitTests.TenantCatalog;

/// <summary>Telugu must cover the portal's English keys — no silent gaps.</summary>
public sealed class PortalStringsTests
{
    [Fact]
    public void Telugu_covers_every_english_key_and_nothing_more()
    {
        PortalStrings.Te.Keys.Should().BeEquivalentTo(PortalStrings.En.Keys);
        PortalStrings.Te.Values.Should().NotContain(string.Empty);
        PortalStrings.En.Values.Should().NotContain(string.Empty);
    }
}
