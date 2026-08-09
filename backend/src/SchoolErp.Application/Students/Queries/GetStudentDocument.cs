using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Students.Queries;

/// <summary>Official documents a school issues for a student.</summary>
public enum StudentDocumentType
{
    TransferCertificate = 1,
    BonafideCertificate = 2,
    IdCard = 3,
}

/// <summary>Everything the document PDFs show, ready for rendering.</summary>
public sealed record StudentDocumentData(
    string SchoolName,
    string? SchoolAddress,
    string? AffiliationBoard,
    string? AffiliationNumber,
    string StudentName,
    string AdmissionNumber,
    DateOnly DateOfBirth,
    Gender Gender,
    DateOnly AdmissionDate,
    string? ClassName,
    string? SectionName,
    int? RollNumber,
    string? AcademicYearName,
    string? GuardianName,
    string? GuardianPhone,
    DateOnly IssuedOn)
{
    /// <summary>Raw image bytes for the ID-card photo; null renders a placeholder.</summary>
    public byte[]? PhotoBytes { get; init; }

    /// <summary>Printed on the back of the ID card — the reason the back exists.</summary>
    public string? BloodGroup { get; init; }

    /// <summary>School's contact number, for the "if found" line on the card back.</summary>
    public string? SchoolPhone { get; init; }
}

/// <summary>Turns document data into a PDF. Implemented in Infrastructure.</summary>
public interface IStudentDocumentRenderer
{
    byte[] Render(StudentDocumentType type, StudentDocumentData data);
}

/// <summary>An official student document as a PDF.</summary>
public sealed record GetStudentDocumentPdfQuery(
    Guid StudentId, StudentDocumentType Type) : IRequest<byte[]>;

/// <summary>Composes student + placement + guardian + school, then renders.</summary>
public sealed class GetStudentDocumentPdfQueryHandler
    : IRequestHandler<GetStudentDocumentPdfQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentDocumentRenderer _renderer;
    private readonly IFileStorage _fileStorage;
    private readonly TimeProvider _clock;

    public GetStudentDocumentPdfQueryHandler(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        IStudentDocumentRenderer renderer,
        IFileStorage fileStorage,
        TimeProvider clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _renderer = renderer;
        _fileStorage = fileStorage;
        _clock = clock;
    }

    public async Task<byte[]> Handle(
        GetStudentDocumentPdfQuery request, CancellationToken cancellationToken)
    {
        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == request.StudentId)
            .Select(s => new
            {
                Name = (s.FirstName + " " + s.LastName).Trim(),
                s.AdmissionNumber,
                s.DateOfBirth,
                s.Gender,
                s.AdmissionDate,
                s.PhotoUrl,
                s.BloodGroup,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var placement = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == request.StudentId && e.AcademicYear!.IsCurrent)
            .Select(e => new
            {
                ClassName = e.SchoolClass!.Name,
                SectionName = e.Section != null ? e.Section.Name : null,
                e.RollNumber,
                YearName = e.AcademicYear!.Name,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var guardian = await _db.StudentGuardians.AsNoTracking()
            .Where(g => g.StudentId == request.StudentId && g.IsPrimary)
            .Select(g => new
            {
                Name = (g.Guardian!.FirstName + " " + g.Guardian.LastName).Trim(),
                g.Guardian.Phone,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var school = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => new
            {
                t.Name,
                t.AddressLine1,
                t.City,
                t.State,
                t.PostalCode,
                t.ContactPhone,
                t.AffiliationBoard,
                t.AffiliationNumber,
            })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        // The postal code matters on the card back, where the address is what
        // a finder posts the card back to.
        var address = string.Join(", ",
            new[] { school.AddressLine1, school.City, school.State, school.PostalCode }
                .Where(p => !string.IsNullOrWhiteSpace(p)));

        // The ID card embeds the stored photo; the renderer works from bytes
        // so it stays storage-agnostic. Missing photos render a placeholder.
        byte[]? photo = null;
        const string filePrefix = "/api/v1/files/";
        if (student.PhotoUrl is { } photoUrl &&
            photoUrl.StartsWith(filePrefix, StringComparison.Ordinal))
        {
            var opened = await _fileStorage
                .OpenAsync(photoUrl[filePrefix.Length..], cancellationToken)
                .ConfigureAwait(false);
            if (opened is { } file)
            {
                await using var stream = file.Content;
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                photo = buffer.ToArray();
            }
        }

        return _renderer.Render(request.Type, new StudentDocumentData(
            school.Name,
            address.Length == 0 ? null : address,
            school.AffiliationBoard,
            school.AffiliationNumber,
            student.Name,
            student.AdmissionNumber,
            student.DateOfBirth,
            student.Gender,
            student.AdmissionDate,
            placement?.ClassName,
            placement?.SectionName,
            placement?.RollNumber,
            placement?.YearName,
            guardian?.Name,
            guardian?.Phone,
            DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime))
        {
            PhotoBytes = photo,
            BloodGroup = student.BloodGroup,
            SchoolPhone = school.ContactPhone,
        });
    }
}
