using System.Linq.Expressions;
using SchoolErp.Domain.Academics;

namespace SchoolErp.Application.Academics;

/// <summary>Academic session projection.</summary>
public sealed record AcademicYearDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IsCurrent { get; init; }
}

/// <summary>Section projection.</summary>
public sealed record SectionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? Capacity { get; init; }
}

/// <summary>Class projection with its sections.</summary>
public sealed record SchoolClassDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }

    /// <summary>The programme this cohort belongs to; null at a school.</summary>
    public Guid? ProgrammeId { get; init; }

    public IReadOnlyList<SectionDto> Sections { get; init; } = [];
}

/// <summary>Hand-written projections (EF-translatable expressions + in-memory maps).</summary>
public static class AcademicsMappings
{
    /// <summary>EF-translatable projection for query composition.</summary>
    public static readonly Expression<Func<AcademicYear, AcademicYearDto>> YearProjection =
        year => new AcademicYearDto
        {
            Id = year.Id,
            Name = year.Name,
            StartDate = year.StartDate,
            EndDate = year.EndDate,
            IsCurrent = year.IsCurrent,
        };

    /// <summary>EF-translatable projection including sections.</summary>
    public static readonly Expression<Func<SchoolClass, SchoolClassDto>> ClassProjection =
        schoolClass => new SchoolClassDto
        {
            Id = schoolClass.Id,
            Name = schoolClass.Name,
            DisplayOrder = schoolClass.DisplayOrder,
            ProgrammeId = schoolClass.ProgrammeId,
            Sections = schoolClass.Sections
                .OrderBy(s => s.Name)
                .Select(s => new SectionDto { Id = s.Id, Name = s.Name, Capacity = s.Capacity })
                .ToList(),
        };

    public static AcademicYearDto ToDto(this AcademicYear year) => new()
    {
        Id = year.Id,
        Name = year.Name,
        StartDate = year.StartDate,
        EndDate = year.EndDate,
        IsCurrent = year.IsCurrent,
    };

    public static SchoolClassDto ToDto(this SchoolClass schoolClass) => new()
    {
        Id = schoolClass.Id,
        Name = schoolClass.Name,
        DisplayOrder = schoolClass.DisplayOrder,
        ProgrammeId = schoolClass.ProgrammeId,
        Sections = schoolClass.Sections
            .OrderBy(s => s.Name)
            .Select(s => new SectionDto { Id = s.Id, Name = s.Name, Capacity = s.Capacity })
            .ToList(),
    };
}
