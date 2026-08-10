using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Academics;

/// <summary>
/// An academic department of a college — Computer Science, Commerce, Physics.
/// Schools do not have these; the portal only offers them to a tenant whose
/// InstitutionType is College.
/// </summary>
public class Department : TenantEntity
{
    /// <summary>Display name, unique within the institution.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code staff use on notices and timetables (e.g. "CSE").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Head of department, if one is recorded. Not required.</summary>
    public Guid? HeadTeacherId { get; set; }

    /// <summary>Closed departments keep their history; they are never deleted.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<Programme> Programmes { get; set; } = [];
}

/// <summary>What a programme awards. Drives nothing yet beyond grouping.</summary>
public enum ProgrammeLevel
{
    Certificate = 1,
    Diploma = 2,
    Undergraduate = 3,
    Postgraduate = 4,
}

/// <summary>
/// A course of study a college runs — "B.Tech Computer Science", "B.Com
/// General". A programme belongs to one department and spans several terms;
/// the cohort actually taught in a given term is an ordinary
/// <see cref="SchoolClass"/> pointed at this programme, so attendance,
/// timetables, exams and fees keep working without a parallel implementation.
/// </summary>
public class Programme : TenantEntity
{
    public Guid DepartmentId { get; set; }

    public Department? Department { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, unique within the institution (e.g. "BTCSE").</summary>
    public string Code { get; set; } = string.Empty;

    public ProgrammeLevel Level { get; set; } = ProgrammeLevel.Undergraduate;

    /// <summary>Length in years — 3 for a B.Com, 4 for a B.Tech.</summary>
    public int DurationYears { get; set; } = 3;

    /// <summary>
    /// Terms in one year: 2 for a semester system, 1 where the whole year is
    /// examined together. Duration × this is how many cohorts a full intake
    /// passes through.
    /// </summary>
    public int TermsPerYear { get; set; } = 2;

    public bool IsActive { get; set; } = true;
}
