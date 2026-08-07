using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Students;

public enum GuardianRelation
{
    Father = 1,
    Mother = 2,
    Guardian = 3,
    Other = 4,
}

/// <summary>
/// A parent/guardian. One guardian can be linked to several students
/// (siblings); the mobile-app account link is via <see cref="UserId"/>.
/// </summary>
public class Guardian : TenantEntity
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public GuardianRelation Relation { get; set; }

    /// <summary>E.164 phone — the parent app login identity.</summary>
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Occupation { get; set; }

    /// <summary>Linked platform user account (set when app access is provisioned).</summary>
    public Guid? UserId { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>Links a student to a guardian; exactly one link per student is primary.</summary>
public class StudentGuardian : TenantEntity
{
    public Guid StudentId { get; set; }

    public Guid GuardianId { get; set; }

    public Guardian? Guardian { get; set; }

    /// <summary>Primary contact for notifications and fee reminders.</summary>
    public bool IsPrimary { get; set; }
}
