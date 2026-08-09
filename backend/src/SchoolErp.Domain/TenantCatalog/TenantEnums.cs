namespace SchoolErp.Domain.TenantCatalog;

/// <summary>Commercial plan a school is subscribed to.</summary>
public enum SubscriptionPlan
{
    Trial = 0,
    Basic = 1,
    Standard = 2,
    Premium = 3,
    Enterprise = 4,
}

/// <summary>Lifecycle state of a tenant.</summary>
public enum TenantStatus
{
    /// <summary>Created but not yet ready to serve traffic.</summary>
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    /// <summary>Retained for statutory data-retention, no longer serving traffic.</summary>
    Archived = 3,
}

/// <summary>
/// Feature modules that can be switched per tenant. Stored as a flags column so
/// entitlement checks are a single bitwise test.
/// </summary>
[Flags]
public enum TenantModules : long
{
    None = 0,
    /// <summary>SIS, attendance, communication — always on.</summary>
    Core = 1 << 0,
    Examination = 1 << 1,
    Fees = 1 << 2,
    Transport = 1 << 3,
    Library = 1 << 4,
    Timetable = 1 << 5,
    Homework = 1 << 6,
    FrontOffice = 1 << 7,
    HumanResources = 1 << 8,
    Inventory = 1 << 9,
    Hostel = 1 << 10,
}

/// <summary>
/// How a school wants its report cards laid out. Indian schools differ:
/// senior classes print raw marks, CBSE primary sections often publish
/// grades only, and some want both.
/// </summary>
public enum ReportCardTemplate
{
    /// <summary>Subject, max marks and obtained — no grade column.</summary>
    MarksOnly = 1,

    /// <summary>Marks plus the grade column (the default).</summary>
    MarksAndGrades = 2,

    /// <summary>Grades only; raw marks are never printed (CBSE primary style).</summary>
    GradesOnly = 3,
}
