using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Homework;

/// <summary>
/// Homework set for a class (optionally narrowed to one section) in a subject.
/// </summary>
public class HomeworkAssignment : TenantEntity
{
    public Guid SchoolClassId { get; set; }

    public SchoolClass? SchoolClass { get; set; }

    /// <summary>Null = every section of the class.</summary>
    public Guid? SectionId { get; set; }

    public Guid SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public DateOnly AssignedOn { get; set; }

    public DateOnly DueDate { get; set; }
}
