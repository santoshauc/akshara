using MudBlazor;

namespace SchoolErp.AdminPortal.Layout;

/// <summary>One destination in the sidebar.</summary>
/// <param name="LabelKey">PortalStrings key — the label is always localized.</param>
/// <param name="Href">Route, relative to the app base. Empty string = dashboard.</param>
/// <param name="Icon">Material outlined glyph; the portal uses one icon family.</param>
/// <param name="PlatformOnly">Shown only to platform operators.</param>
/// <param name="SchoolOnly">Hidden from platform operators (school-scoped data).</param>
public sealed record NavItem(
    string LabelKey,
    string Href,
    string Icon,
    bool PlatformOnly = false,
    bool SchoolOnly = false);

/// <summary>A labelled group of destinations.</summary>
public sealed record NavSection(string LabelKey, IReadOnlyList<NavItem> Items);

/// <summary>
/// The portal's navigation, grouped by what someone is trying to do rather
/// than by which team built the module. A flat list of twenty-odd links is
/// unusable; six short groups can be scanned at a glance.
///
/// Only modules that actually exist appear here.
/// </summary>
public static class NavModel
{
    private static readonly NavSection[] All =
    [
        new("nav.group.overview",
        [
            new("nav.dashboard", "", Icons.Material.Outlined.SpaceDashboard),
            new("nav.myDay", "my-day", Icons.Material.Outlined.Today, SchoolOnly: true),
            new("nav.insights", "insights", Icons.Material.Outlined.Insights, SchoolOnly: true),
        ]),

        new("nav.group.academics",
        [
            new("nav.academics", "academics", Icons.Material.Outlined.CalendarMonth, SchoolOnly: true),
            new("nav.timetable", "timetable", Icons.Material.Outlined.GridView, SchoolOnly: true),
            new("nav.exams", "exams", Icons.Material.Outlined.Assignment, SchoolOnly: true),
            new("nav.homework", "homework", Icons.Material.Outlined.MenuBook, SchoolOnly: true),
            new("nav.attendance", "attendance", Icons.Material.Outlined.FactCheck, SchoolOnly: true),
        ]),

        new("nav.group.people",
        [
            new("nav.students", "students", Icons.Material.Outlined.Groups, SchoolOnly: true),
            new("nav.admissions", "admissions", Icons.Material.Outlined.PersonAddAlt, SchoolOnly: true),
            new("nav.teachers", "teachers", Icons.Material.Outlined.Badge, SchoolOnly: true),
            new("nav.leave", "leave", Icons.Material.Outlined.EventBusy, SchoolOnly: true),
        ]),

        new("nav.group.operations",
        [
            new("nav.transport", "transport", Icons.Material.Outlined.DirectionsBus, SchoolOnly: true),
            new("nav.frontOffice", "front-office", Icons.Material.Outlined.HowToReg, SchoolOnly: true),
            new("nav.inventory", "inventory", Icons.Material.Outlined.Inventory2, SchoolOnly: true),
            new("nav.library", "library", Icons.Material.Outlined.LocalLibrary, SchoolOnly: true),
            new("nav.hostel", "hostel", Icons.Material.Outlined.Bed, SchoolOnly: true),
        ]),

        new("nav.group.communication",
        [
            new("nav.notices", "notices", Icons.Material.Outlined.Campaign, SchoolOnly: true),
            new("nav.messages", "messages", Icons.Material.Outlined.Forum, SchoolOnly: true),
        ]),

        new("nav.group.finance",
        [
            new("nav.fees", "fees", Icons.Material.Outlined.Payments, SchoolOnly: true),
            new("nav.subscription", "subscription", Icons.Material.Outlined.CardMembership, SchoolOnly: true),
        ]),

        new("nav.group.administration",
        [
            new("nav.schools", "tenants", Icons.Material.Outlined.Domain, PlatformOnly: true),
            new("nav.campuses", "campuses", Icons.Material.Outlined.Apartment, SchoolOnly: true),
            new("nav.users", "users", Icons.Material.Outlined.ManageAccounts, SchoolOnly: true),
            new("nav.audit", "audit", Icons.Material.Outlined.History, SchoolOnly: true),
            new("nav.operators", "platform/operators", Icons.Material.Outlined.AdminPanelSettings, PlatformOnly: true),
            new("nav.operatorLog", "platform/audit", Icons.Material.Outlined.Policy, PlatformOnly: true),
        ]),
    ];

    /// <summary>
    /// Sections visible to this principal, with empty groups dropped — a
    /// platform operator should not see an "Academics" heading with nothing
    /// under it.
    /// </summary>
    public static IReadOnlyList<NavSection> For(bool isPlatformUser)
    {
        var result = new List<NavSection>(All.Length);
        foreach (var section in All)
        {
            var items = section.Items
                .Where(i => (!i.PlatformOnly || isPlatformUser) &&
                            (!i.SchoolOnly || !isPlatformUser))
                .ToList();
            if (items.Count > 0)
            {
                result.Add(section with { Items = items });
            }
        }

        return result;
    }
}
