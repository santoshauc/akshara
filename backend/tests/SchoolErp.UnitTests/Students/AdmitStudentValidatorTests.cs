using FluentAssertions;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Students;

namespace SchoolErp.UnitTests.Students;

/// <summary>Shape-rule coverage for the admission command.</summary>
public sealed class AdmitStudentValidatorTests
{
    private readonly AdmitStudentCommandValidator _validator = new(TimeProvider.System);

    private static AdmitStudentCommand Valid(params GuardianInput[] guardians) => new(
        AdmissionNumber: null,
        FirstName: "Aarav",
        LastName: "Sharma",
        DateOfBirth: new DateOnly(2016, 4, 12),
        Gender: Gender.Male,
        BloodGroup: "B+",
        Email: null,
        Phone: null,
        AddressLine1: null,
        City: null,
        State: null,
        PostalCode: null,
        MedicalNotes: null,
        AdmissionDate: new DateOnly(2026, 6, 10),
        AcademicYearId: Guid.NewGuid(),
        SchoolClassId: Guid.NewGuid(),
        SectionId: Guid.NewGuid(),
        RollNumber: null,
        Guardians: guardians.Length > 0
            ? guardians
            : [new GuardianInput("Rakesh", "Sharma", GuardianRelation.Father, "+919812345670", null, null, true)]);

    [Fact]
    public void Accepts_a_valid_admission() =>
        _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void Rejects_future_date_of_birth() =>
        _validator.Validate(Valid() with { DateOfBirth = new DateOnly(2100, 1, 1) })
            .IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_admission_without_guardians() =>
        _validator.Validate(Valid() with { Guardians = [] }).IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_more_than_one_primary_guardian()
    {
        var result = _validator.Validate(Valid(
            new GuardianInput("A", "B", GuardianRelation.Father, "+919812345671", null, null, true),
            new GuardianInput("C", "D", GuardianRelation.Mother, "+919812345672", null, null, true)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("primary"));
    }

    [Fact]
    public void Rejects_invalid_guardian_phone() =>
        _validator.Validate(Valid(
            new GuardianInput("A", "B", GuardianRelation.Father, "12345", null, null, true)))
            .IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_zero_roll_number() =>
        _validator.Validate(Valid() with { RollNumber = 0 }).IsValid.Should().BeFalse();
}
