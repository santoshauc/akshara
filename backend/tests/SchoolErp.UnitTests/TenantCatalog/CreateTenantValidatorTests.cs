using FluentAssertions;
using SchoolErp.Application.TenantCatalog;
using SchoolErp.Application.TenantCatalog.Commands;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.UnitTests.TenantCatalog;

/// <summary>Shape-rule coverage for school onboarding.</summary>
public sealed class CreateTenantValidatorTests
{
    private readonly CreateTenantCommandValidator _validator = new();

    private static CreateTenantCommand Valid() => new(
        Code: "GRWD01",
        Name: "Greenwood High",
        Subdomain: "greenwood",
        CustomDomain: null,
        ContactEmail: "office@greenwood.edu.in",
        ContactPhone: "+919876543210",
        City: "Hyderabad",
        State: "Telangana",
        Affiliations: [new TenantAffiliationDto("CBSE", "1234567")],
        Plan: SubscriptionPlan.Standard,
        EnabledModules: TenantModules.Core | TenantModules.Fees);

    [Fact]
    public void Accepts_a_fully_valid_command() =>
        _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("abc")]        // too short
    [InlineData("grwd01")]     // lowercase
    [InlineData("TOOLONGCODE")] // > 8 chars
    [InlineData("GR WD1")]     // whitespace
    public void Rejects_invalid_school_codes(string code) =>
        _validator.Validate(Valid() with { Code = code }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("ab")]              // too short
    [InlineData("Greenwood")]       // uppercase
    [InlineData("-greenwood")]      // leading hyphen
    [InlineData("green wood")]      // whitespace
    public void Rejects_invalid_subdomains(string subdomain) =>
        _validator.Validate(Valid() with { Subdomain = subdomain }).IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_disabling_the_core_module() =>
        _validator.Validate(Valid() with { EnabledModules = TenantModules.Fees })
            .IsValid.Should().BeFalse();

    [Fact]
    public void Rejects_unsupported_languages() =>
        _validator.Validate(Valid() with { DefaultLanguage = "fr" })
            .IsValid.Should().BeFalse();

    [Fact]
    public void Accepts_telugu_as_default_language() =>
        _validator.Validate(Valid() with { DefaultLanguage = "te" })
            .IsValid.Should().BeTrue();
}
