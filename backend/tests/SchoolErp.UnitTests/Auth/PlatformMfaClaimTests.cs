using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SchoolErp.Infrastructure.Identity;

namespace SchoolErp.UnitTests.Auth;

/// <summary>
/// The platform MFA gate is a config switch, and Development turns it off so
/// the seeded demo operator is usable. These pin the switch: default ON, and
/// off only when something explicitly says so — a silent flip to false would
/// disarm production without failing anything else.
/// </summary>
public sealed class PlatformMfaClaimTests
{
    private const string Claim = "platform_mfa_setup_required";

    private static JwtTokenService ServiceWith(bool requireMfa) =>
        new(Options.Create(new JwtOptions
        {
            SigningKey = "unit-test-signing-key-0123456789abcdef",
            RequirePlatformMfa = requireMfa,
        }), TimeProvider.System);

    private static ApplicationUser PlatformUserWithoutMfa() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "opaque",
        FullName = "Platform Operator",
        TenantId = null,
        TwoFactorEnabled = false,
    };

    private static IEnumerable<string> ClaimTypes(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Select(c => c.Type);

    [Fact]
    public void The_gate_is_on_by_default()
    {
        new JwtOptions().RequirePlatformMfa.Should().BeTrue(
            "an operator can edit every school; MFA is not opt-in");
    }

    [Fact]
    public void An_unenrolled_operator_is_marked_when_the_gate_is_on()
    {
        var token = ServiceWith(true).CreateAccessToken(PlatformUserWithoutMfa(), [], []);

        ClaimTypes(token).Should().Contain(Claim);
    }

    [Fact]
    public void The_mark_is_absent_when_the_gate_is_off()
    {
        var token = ServiceWith(false).CreateAccessToken(PlatformUserWithoutMfa(), [], []);

        ClaimTypes(token).Should().NotContain(Claim);
    }

    [Fact]
    public void A_school_account_is_never_marked_whatever_the_gate_says()
    {
        var schoolUser = PlatformUserWithoutMfa();
        schoolUser.TenantId = Guid.NewGuid();

        var token = ServiceWith(true).CreateAccessToken(schoolUser, [], [], "DEMO01");

        ClaimTypes(token).Should().NotContain(Claim);
        ClaimTypes(token).Should().Contain("tenant").And.Contain("school_code");
    }
}
