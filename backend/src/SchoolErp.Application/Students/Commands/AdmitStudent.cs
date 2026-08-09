using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;
using SchoolErp.Shared.Localization;

namespace SchoolErp.Application.Students.Commands;

/// <summary>
/// Admits a student: creates the record, links guardians (reusing existing
/// guardians matched by phone so siblings share one parent record), and
/// enrolls them into a class/section for an academic year.
/// </summary>
public sealed record AdmitStudentCommand(
    string? AdmissionNumber,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? BloodGroup,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? MedicalNotes,
    DateOnly AdmissionDate,
    Guid AcademicYearId,
    Guid SchoolClassId,
    Guid SectionId,
    int? RollNumber,
    IReadOnlyList<GuardianInput> Guardians) : IRequest<Guid>;

/// <summary>Admission shape rules.</summary>
public sealed class AdmitStudentCommandValidator : AbstractValidator<AdmitStudentCommand>
{
    public AdmitStudentCommandValidator(TimeProvider clock)
    {
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(64);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(64);

        RuleFor(c => c.DateOfBirth)
            .Must(dob => dob < DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            .WithMessage("Date of birth must be in the past.");

        RuleFor(c => c.AdmissionNumber).MaximumLength(32);
        RuleFor(c => c.Email).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Phone).Matches(@"^\+?[0-9]{10,15}$")
            .When(c => !string.IsNullOrWhiteSpace(c.Phone));
        RuleFor(c => c.RollNumber).GreaterThan(0).When(c => c.RollNumber.HasValue);

        RuleFor(c => c.Guardians).NotEmpty()
            .WithMessage("At least one guardian is required.")
            .Must(g => g.Count(x => x.IsPrimary) == 1)
            .WithMessage("Exactly one guardian must be marked primary.");

        RuleForEach(c => c.Guardians).ChildRules(g =>
        {
            g.RuleFor(x => x.FirstName).NotEmpty().MaximumLength(64);
            g.RuleFor(x => x.LastName).NotEmpty().MaximumLength(64);
            g.RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+?[0-9]{10,15}$")
                .WithMessage("Guardian phone must be a valid 10–15 digit number.");
            g.RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        });
    }
}

/// <summary>Runs the admission workflow in one transaction.</summary>
public sealed class AdmitStudentCommandHandler : IRequestHandler<AdmitStudentCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public AdmitStudentCommandHandler(
        IApplicationDbContext db, ITenantContext tenantContext, TimeProvider clock)
    {
        _db = db;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Guid> Handle(AdmitStudentCommand request, CancellationToken cancellationToken)
    {
        await EnsurePlacementExistsAsync(request, cancellationToken).ConfigureAwait(false);

        var admissionNumber = string.IsNullOrWhiteSpace(request.AdmissionNumber)
            ? await GenerateAdmissionNumberAsync(request.AdmissionDate, cancellationToken).ConfigureAwait(false)
            : request.AdmissionNumber.Trim().ToUpperInvariant();

        // IgnoreQueryFilters: DPDP-erased students keep their numbers reserved.
        if (await _db.Students.IgnoreQueryFilters()
                .AnyAsync(s => s.TenantId == _tenantContext.TenantId &&
                               s.AdmissionNumber == admissionNumber, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"Admission number '{admissionNumber}' is already in use.");
        }

        var student = new Student
        {
            AdmissionNumber = admissionNumber,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            BloodGroup = request.BloodGroup?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            AddressLine1 = request.AddressLine1?.Trim(),
            City = request.City?.Trim(),
            State = request.State?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            MedicalNotes = request.MedicalNotes?.Trim(),
            AdmissionDate = request.AdmissionDate,
        };
        _db.Students.Add(student);

        foreach (var input in request.Guardians)
        {
            var guardian = await FindOrCreateGuardianAsync(input, cancellationToken).ConfigureAwait(false);
            _db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                IsPrimary = input.IsPrimary,
            });
        }

        _db.Enrollments.Add(new Enrollment
        {
            StudentId = student.Id,
            AcademicYearId = request.AcademicYearId,
            SchoolClassId = request.SchoolClassId,
            SectionId = request.SectionId,
            RollNumber = request.RollNumber,
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return student.Id;
    }

    /// <summary>Validates the year/class/section combination against the database.</summary>
    private async Task EnsurePlacementExistsAsync(AdmitStudentCommand request, CancellationToken ct)
    {
        if (!await _db.AcademicYears.AnyAsync(y => y.Id == request.AcademicYearId, ct).ConfigureAwait(false))
        {
            throw new NotFoundException("AcademicYear", request.AcademicYearId);
        }

        var sectionBelongsToClass = await _db.Sections
            .AnyAsync(s => s.Id == request.SectionId && s.SchoolClassId == request.SchoolClassId, ct)
            .ConfigureAwait(false);
        if (!sectionBelongsToClass)
        {
            throw new NotFoundException("Section (in the given class)", request.SectionId);
        }
    }

    /// <summary>
    /// Guardians are deduplicated by phone within the tenant so sibling
    /// admissions attach to the same parent record (and later, the same
    /// parent-app account).
    /// </summary>
    private async Task<Guardian> FindOrCreateGuardianAsync(GuardianInput input, CancellationToken ct)
    {
        var phone = input.Phone.Trim();
        var existing = await _db.Guardians
            .FirstOrDefaultAsync(g => g.Phone == phone, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            // A sibling admission may correct the parent's language; anything
            // else about the existing record is left alone on purpose.
            if (NotificationLanguages.IsSupported(input.PreferredLanguage))
            {
                existing.PreferredLanguage = NotificationLanguages.Normalize(input.PreferredLanguage);
            }

            return existing;
        }

        var guardian = new Guardian
        {
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            Relation = input.Relation,
            Phone = phone,
            Email = input.Email?.Trim(),
            Occupation = input.Occupation?.Trim(),
            PreferredLanguage = NotificationLanguages.Normalize(input.PreferredLanguage),
        };
        _db.Guardians.Add(guardian);
        return guardian;
    }

    /// <summary>
    /// Sequential per-year admission numbers: "ADM-2026-0001". The per-tenant
    /// unique index is the final referee under concurrency; a clash surfaces
    /// as a 409 and the operator simply retries.
    /// </summary>
    private async Task<string> GenerateAdmissionNumberAsync(DateOnly admissionDate, CancellationToken ct)
    {
        var year = admissionDate.Year;
        var prefix = $"ADM-{year}-";
        // IgnoreQueryFilters + explicit tenant scope: DPDP-erased students are
        // soft-deleted and invisible to the default filter, but their admission
        // numbers stay reserved — counting without them would reissue numbers.
        var count = await _db.Students
            .IgnoreQueryFilters()
            .CountAsync(s => s.TenantId == _tenantContext.TenantId &&
                             s.AdmissionNumber.StartsWith(prefix), ct)
            .ConfigureAwait(false);
        return $"{prefix}{count + 1:D4}";
    }
}
