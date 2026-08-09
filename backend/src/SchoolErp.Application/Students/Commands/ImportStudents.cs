using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Students.Commands;

/// <summary>One data row lifted from the uploaded sheet (1-based row number).</summary>
public sealed record StudentImportRow(
    int RowNumber,
    string? AdmissionNumber,
    string? FirstName,
    string? LastName,
    string? DateOfBirth,
    string? Gender,
    string? ClassName,
    string? SectionName,
    string? RollNumber,
    string? AdmissionDate,
    string? BloodGroup,
    string? City,
    string? State,
    string? GuardianFirstName,
    string? GuardianLastName,
    string? GuardianRelation,
    string? GuardianPhone,
    string? GuardianEmail);

/// <summary>What the template builder needs to tailor the workbook.</summary>
public sealed record ImportTemplateContext(
    string SchoolName,
    IReadOnlyList<(string ClassName, IReadOnlyList<string> Sections)> Classes);

/// <summary>
/// Reads and writes the student-import workbook. Implemented in
/// Infrastructure (ClosedXML) — the application layer stays format-agnostic.
/// </summary>
public interface IStudentImportWorkbook
{
    /// <summary>The downloadable template, tailored with the school's classes.</summary>
    byte[] BuildTemplate(ImportTemplateContext context);

    /// <summary>
    /// Lifts data rows from an uploaded workbook. Throws
    /// <see cref="ValidationException"/> when the file isn't the template.
    /// </summary>
    IReadOnlyList<StudentImportRow> Parse(byte[] content);
}

/// <summary>One rejected row and why.</summary>
public sealed record ImportRowError(int RowNumber, string Message);

/// <summary>The import outcome: either everything landed or nothing did.</summary>
public sealed record ImportStudentsResultDto(
    int TotalRows,
    int Imported,
    IReadOnlyList<ImportRowError> Errors);

/// <summary>The template, tailored to the caller's school.</summary>
public sealed record GetStudentImportTemplateQuery : IRequest<byte[]>;

/// <summary>Builds the workbook with the school's real classes and sections.</summary>
public sealed class GetStudentImportTemplateQueryHandler
    : IRequestHandler<GetStudentImportTemplateQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentImportWorkbook _workbook;

    public GetStudentImportTemplateQueryHandler(
        IApplicationDbContext db, ITenantContext tenantContext, IStudentImportWorkbook workbook)
    {
        _db = db;
        _tenantContext = tenantContext;
        _workbook = workbook;
    }

    public async Task<byte[]> Handle(
        GetStudentImportTemplateQuery request, CancellationToken cancellationToken)
    {
        var schoolName = await _db.Tenants
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? "School";

        var classes = await _db.SchoolClasses
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                c.Name,
                Sections = c.Sections.OrderBy(s => s.Name).Select(s => s.Name).ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return _workbook.BuildTemplate(new ImportTemplateContext(
            schoolName,
            classes.Select(c => (c.Name, (IReadOnlyList<string>)c.Sections)).ToList()));
    }
}

/// <summary>
/// Imports an uploaded student workbook. Every row is validated first; the
/// file is rejected as a whole when any row is invalid, so a school never
/// ends up half-imported. Valid files are admitted row by row through the
/// normal admission pipeline (guardian reuse by phone, generated admission
/// numbers, audit trail).
/// </summary>
public sealed record ImportStudentsCommand(byte[] Content) : IRequest<ImportStudentsResultDto>;

/// <summary>Parses, validates and admits.</summary>
public sealed partial class ImportStudentsCommandHandler
    : IRequestHandler<ImportStudentsCommand, ImportStudentsResultDto>
{
    private const int MaxRows = 1_000;

    private readonly IApplicationDbContext _db;
    private readonly IStudentImportWorkbook _workbook;
    private readonly ISender _sender;
    private readonly ITenantContext _tenantContext;

    public ImportStudentsCommandHandler(
        IApplicationDbContext db, IStudentImportWorkbook workbook, ISender sender,
        ITenantContext tenantContext)
    {
        _db = db;
        _workbook = workbook;
        _sender = sender;
        _tenantContext = tenantContext;
    }

    public async Task<ImportStudentsResultDto> Handle(
        ImportStudentsCommand request, CancellationToken cancellationToken)
    {
        var rows = _workbook.Parse(request.Content);
        if (rows.Count == 0)
        {
            throw new ValidationException(
                "The sheet has no data rows. Fill the Students sheet and upload again.");
        }

        if (rows.Count > MaxRows)
        {
            throw new ValidationException(
                $"The sheet has {rows.Count} rows; the limit per upload is {MaxRows}.");
        }

        var yearId = await _db.AcademicYears
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ValidationException(
                "No current academic year is set. Create one under Academics first.");

        var sections = await _db.SchoolClasses
            .SelectMany(c => c.Sections.Select(s => new
            {
                ClassName = c.Name,
                SectionName = s.Name,
                ClassId = c.Id,
                SectionId = s.Id,
            }))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var sectionLookup = sections.ToDictionary(
            s => (Normalize(s.ClassName), Normalize(s.SectionName)),
            s => (s.ClassId, s.SectionId));

        // Erased (soft-deleted) students keep their numbers reserved, so the
        // duplicate check must bypass the global filters — same rule as the
        // admission-number generator (see DPDP notes).
        var tenantId = _tenantContext.TenantId;
        var existingAdmissionNumbers = (await _db.Students
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId)
                .Select(s => s.AdmissionNumber)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Guards a re-uploaded file from creating duplicate children. Giving
        // the row an explicit (new) admission number is the escape hatch for
        // genuine same-name-same-birthday students.
        var existingPeople = (await _db.Students
                .Select(s => new { s.FirstName, s.LastName, s.DateOfBirth })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(s => (Normalize(s.FirstName), Normalize(s.LastName), s.DateOfBirth))
            .ToHashSet();

        var parsed = new List<(StudentImportRow Row, AdmitStudentCommand Command)>();
        var errors = new List<ImportRowError>();
        var admissionNumbersInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var rowErrors = new List<string>();
            var command = TryBuildCommand(
                row, yearId, sectionLookup, existingAdmissionNumbers,
                admissionNumbersInFile, existingPeople, rowErrors);
            if (command is null)
            {
                errors.AddRange(rowErrors.Select(e => new ImportRowError(row.RowNumber, e)));
            }
            else
            {
                parsed.Add((row, command));
            }
        }

        if (errors.Count > 0)
        {
            // All-or-nothing: fix the listed rows and upload the file again.
            return new ImportStudentsResultDto(rows.Count, 0, errors);
        }

        var imported = 0;
        foreach (var (row, command) in parsed)
        {
            try
            {
                await _sender.Send(command, cancellationToken).ConfigureAwait(false);
                imported++;
            }
            catch (Exception ex) when (ex is ValidationException or ConflictException)
            {
                errors.Add(new ImportRowError(row.RowNumber, ex.Message));
            }
        }

        return new ImportStudentsResultDto(rows.Count, imported, errors);
    }

    private static AdmitStudentCommand? TryBuildCommand(
        StudentImportRow row,
        Guid yearId,
        Dictionary<(string, string), (Guid ClassId, Guid SectionId)> sectionLookup,
        HashSet<string> existingAdmissionNumbers,
        HashSet<string> admissionNumbersInFile,
        HashSet<(string First, string Last, DateOnly Dob)> existingPeople,
        List<string> errors)
    {
        void Require(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{field} is required.");
            }
        }

        Require(row.FirstName, "First name");
        Require(row.LastName, "Last name");
        Require(row.GuardianFirstName, "Guardian first name");
        Require(row.GuardianLastName, "Guardian last name");

        DateOnly dateOfBirth = default;
        if (string.IsNullOrWhiteSpace(row.DateOfBirth))
        {
            errors.Add("Date of birth is required.");
        }
        else if (!DateOnly.TryParse(row.DateOfBirth, out dateOfBirth))
        {
            errors.Add($"Date of birth '{row.DateOfBirth}' is not a date (use YYYY-MM-DD).");
        }

        var gender = ParseGender(row.Gender);
        if (gender is null)
        {
            errors.Add($"Gender '{row.Gender}' must be Male, Female or Other.");
        }

        (Guid ClassId, Guid SectionId) placement = default;
        if (string.IsNullOrWhiteSpace(row.ClassName) || string.IsNullOrWhiteSpace(row.SectionName))
        {
            errors.Add("Class and Section are required.");
        }
        else if (!sectionLookup.TryGetValue(
                     (Normalize(row.ClassName), Normalize(row.SectionName)), out placement))
        {
            errors.Add(
                $"Class '{row.ClassName}' section '{row.SectionName}' does not exist " +
                "in this school — check the Instructions sheet for the valid list.");
        }

        int? rollNumber = null;
        if (!string.IsNullOrWhiteSpace(row.RollNumber))
        {
            if (int.TryParse(row.RollNumber, out var roll) && roll > 0)
            {
                rollNumber = roll;
            }
            else
            {
                errors.Add($"Roll number '{row.RollNumber}' must be a positive number.");
            }
        }

        var admissionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(row.AdmissionDate) &&
            !DateOnly.TryParse(row.AdmissionDate, out admissionDate))
        {
            errors.Add($"Admission date '{row.AdmissionDate}' is not a date (use YYYY-MM-DD).");
        }

        var relation = ParseRelation(row.GuardianRelation);
        if (relation is null)
        {
            errors.Add(
                $"Guardian relation '{row.GuardianRelation}' must be Father, Mother, Guardian or Other.");
        }

        var phone = row.GuardianPhone?.Trim().Replace(" ", "", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(phone) || !PhonePattern().IsMatch(phone))
        {
            errors.Add(
                $"Guardian phone '{row.GuardianPhone}' must be 10–15 digits, optionally starting with +.");
        }

        var admissionNumber = string.IsNullOrWhiteSpace(row.AdmissionNumber)
            ? null
            : row.AdmissionNumber.Trim();
        if (admissionNumber is not null)
        {
            if (existingAdmissionNumbers.Contains(admissionNumber))
            {
                errors.Add($"Admission number '{admissionNumber}' is already used in this school.");
            }
            else if (!admissionNumbersInFile.Add(admissionNumber))
            {
                errors.Add($"Admission number '{admissionNumber}' appears twice in the file.");
            }
        }

        if (admissionNumber is null && dateOfBirth != default &&
            !string.IsNullOrWhiteSpace(row.FirstName) &&
            !string.IsNullOrWhiteSpace(row.LastName) &&
            existingPeople.Contains(
                (Normalize(row.FirstName), Normalize(row.LastName), dateOfBirth)))
        {
            errors.Add(
                $"{row.FirstName.Trim()} {row.LastName.Trim()} (born {dateOfBirth:yyyy-MM-dd}) " +
                "already exists — remove the row, or give it an explicit admission number " +
                "if this really is a different child.");
        }

        if (errors.Count > 0)
        {
            return null;
        }

        // Also traps the same child appearing twice within this file.
        existingPeople.Add((Normalize(row.FirstName), Normalize(row.LastName), dateOfBirth));
        return new AdmitStudentCommand(
            admissionNumber,
            row.FirstName!.Trim(),
            row.LastName!.Trim(),
            dateOfBirth,
            gender!.Value,
            string.IsNullOrWhiteSpace(row.BloodGroup) ? null : row.BloodGroup.Trim(),
            null, null, null,
            string.IsNullOrWhiteSpace(row.City) ? null : row.City.Trim(),
            string.IsNullOrWhiteSpace(row.State) ? null : row.State.Trim(),
            null, null,
            admissionDate,
            yearId,
            placement.ClassId,
            placement.SectionId,
            rollNumber,
            [new GuardianInput(
                row.GuardianFirstName!.Trim(),
                row.GuardianLastName!.Trim(),
                relation!.Value,
                phone!,
                string.IsNullOrWhiteSpace(row.GuardianEmail) ? null : row.GuardianEmail.Trim(),
                null,
                IsPrimary: true)]);
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    private static Gender? ParseGender(string? value) =>
        Normalize(value) switch
        {
            "MALE" or "M" or "BOY" => Gender.Male,
            "FEMALE" or "F" or "GIRL" => Gender.Female,
            "OTHER" => Gender.Other,
            _ => null,
        };

    private static GuardianRelation? ParseRelation(string? value) =>
        Normalize(value) switch
        {
            "FATHER" => GuardianRelation.Father,
            "MOTHER" => GuardianRelation.Mother,
            "GUARDIAN" => GuardianRelation.Guardian,
            "OTHER" => GuardianRelation.Other,
            _ => null,
        };

    [GeneratedRegex(@"^\+?[0-9]{10,15}$")]
    private static partial Regex PhonePattern();
}
