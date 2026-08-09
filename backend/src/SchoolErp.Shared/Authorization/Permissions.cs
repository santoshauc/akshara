using System.Reflection;

namespace SchoolErp.Shared.Authorization;

/// <summary>
/// The platform permission catalog. Authorization is permission-based
/// everywhere — endpoints demand a permission, never a role name; roles are
/// just named bundles of these values, editable per tenant.
/// </summary>
public static class Permissions
{
    /// <summary>Claim type under which permissions are embedded in access tokens.</summary>
    public const string ClaimType = "permission";

    public static class TenantCatalog
    {
        public const string View = "tenants.view";
        public const string Manage = "tenants.manage";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Manage = "users.manage";
    }

    public static class Students
    {
        public const string View = "students.view";
        public const string Manage = "students.manage";
    }

    public static class Attendance
    {
        public const string View = "attendance.view";
        public const string Mark = "attendance.mark";
    }

    public static class Examinations
    {
        public const string View = "exams.view";
        public const string Manage = "exams.manage";
        public const string EnterMarks = "exams.enter-marks";
        public const string PublishResults = "exams.publish-results";
    }

    public static class Fees
    {
        public const string View = "fees.view";
        public const string Collect = "fees.collect";
        public const string Configure = "fees.configure";
    }

    public static class Transport
    {
        public const string View = "transport.view";
        public const string Manage = "transport.manage";
    }

    public static class Library
    {
        public const string View = "library.view";
        public const string Manage = "library.manage";
    }

    public static class Hostel
    {
        public const string View = "hostel.view";
        public const string Manage = "hostel.manage";
    }

    public static class Communication
    {
        public const string View = "communication.view";
        public const string Send = "communication.send";
    }

    public static class Homework
    {
        public const string View = "homework.view";
        public const string Manage = "homework.manage";
    }

    public static class Staff
    {
        public const string View = "staff.view";
        public const string Manage = "staff.manage";
    }

    public static class Timetable
    {
        public const string View = "timetable.view";
        public const string Manage = "timetable.manage";
    }

    public static class Academics
    {
        public const string View = "academics.view";
        public const string Manage = "academics.manage";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class Leave
    {
        public const string View = "leave.view";
        public const string Manage = "leave.manage";
    }

    public static class Admissions
    {
        public const string View = "admissions.view";
        public const string Manage = "admissions.manage";
    }

    public static class FrontOffice
    {
        public const string View = "frontoffice.view";
        public const string Manage = "frontoffice.manage";
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
        public const string Manage = "inventory.manage";
    }

    public static class Insights
    {
        public const string View = "insights.view";
    }

    /// <summary>Campuses — a school or college can run several.</summary>
    public static class Campuses
    {
        public const string View = "campuses.view";
        public const string Manage = "campuses.manage";
    }

    public static class Subscription
    {
        /// <summary>See the school's own plan, modules and invoices.</summary>
        public const string View = "subscription.view";
    }

    /// <summary>Every permission constant, discovered once via reflection.</summary>
    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    /// <summary>
    /// Platform-operator permissions. These must NEVER land on a school role:
    /// the endpoints they guard cross tenant boundaries (school catalog,
    /// platform billing). Kept out of school seeds/backfills and rejected by
    /// role editing; the API additionally requires a platform principal.
    /// </summary>
    public static IReadOnlyList<string> PlatformOnly { get; } =
        [TenantCatalog.View, TenantCatalog.Manage];

    /// <summary>What a school role may hold: everything except platform-only.</summary>
    public static IReadOnlyList<string> TenantAssignable { get; } =
        All.Except(PlatformOnly, StringComparer.Ordinal).ToList();
}
