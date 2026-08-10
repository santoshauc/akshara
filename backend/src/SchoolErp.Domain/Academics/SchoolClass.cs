using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Academics;

/// <summary>A grade/standard (e.g. "Grade 5"). Sections partition it.</summary>
public class SchoolClass : TenantEntity
{
    /// <summary>Display name, unique within the tenant (e.g. "Grade 5").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sort order in lists (Nursery … Grade 12).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// The programme this cohort belongs to, for colleges — "Semester 3" only
    /// means something under "B.Tech CSE". Null for schools, where a class is
    /// the whole story. Optional on purpose: making colleges reuse the class
    /// machinery is what keeps attendance, timetables, exams and fees working
    /// for them without a second implementation of each.
    /// </summary>
    public Guid? ProgrammeId { get; set; }

    public Programme? Programme { get; set; }

    public ICollection<Section> Sections { get; set; } = [];
}

/// <summary>A division of a class (e.g. "Grade 5 – A").</summary>
public class Section : TenantEntity
{
    public Guid SchoolClassId { get; set; }

    /// <summary>Short name, unique within its class (e.g. "A").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional seat cap used by admission/allocation screens.</summary>
    public int? Capacity { get; set; }
}
