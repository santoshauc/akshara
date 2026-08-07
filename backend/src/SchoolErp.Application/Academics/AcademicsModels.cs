using AutoMapper;
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
    public IReadOnlyList<SectionDto> Sections { get; init; } = [];
}

/// <summary>AutoMapper profile for academic structure.</summary>
public sealed class AcademicsProfile : Profile
{
    public AcademicsProfile()
    {
        CreateMap<AcademicYear, AcademicYearDto>();
        CreateMap<Section, SectionDto>();
        CreateMap<SchoolClass, SchoolClassDto>();
    }
}
