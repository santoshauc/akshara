using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Auth;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.IntegrationTests.Auth;

/// <summary>
/// End-to-end authentication flows against real PostgreSQL + Identity:
/// password login, claims embedding, lockout, OTP, and refresh rotation with
/// reuse detection.
/// </summary>
public sealed class AuthFlowTests : IClassFixture<AuthTestFixture>
{
    private readonly AuthTestFixture _fixture;

    public AuthFlowTests(AuthTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Login_WithValidPassword_ReturnsTokensWithTenantAndPermissions()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword, "127.0.0.1");

        result.Succeeded.Should().BeTrue();
        result.Tokens!.RefreshToken.Should().NotBeNullOrEmpty();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Tokens.AccessToken);
        jwt.Claims.Should().Contain(c =>
            c.Type == "tenant" && c.Value == _fixture.TenantId.ToString());
        jwt.Claims.Should().Contain(c =>
            c.Type == Permissions.ClaimType && c.Value == Permissions.Users.View);
    }

    [Fact]
    public async Task Login_WithWrongSchoolCode_Fails()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await auth.LoginWithPasswordAsync(
            "NOPE99", AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword, null);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(AuthError.SchoolNotFound);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksTheAccount()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        AuthResult last = AuthResult.Fail(AuthError.None);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await auth.LoginWithPasswordAsync(
                AuthTestFixture.SchoolCode, AuthTestFixture.LockoutEmail, "Wrong@Password1", null);
        }

        last.Error.Should().Be(AuthError.LockedOut, "the fifth failure must trigger lockout");

        // Even the correct password is rejected while locked out.
        var correct = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.LockoutEmail, AuthTestFixture.AdminPassword, null);
        correct.Error.Should().Be(AuthError.LockedOut);
    }

    [Fact]
    public async Task Refresh_RotatesTokens_AndReuseRevokesTheWholeFamily()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var login = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword, null);
        var original = login.Tokens!.RefreshToken;

        // Normal rotation succeeds and yields a different token.
        var rotated = await auth.RefreshAsync(original, null);
        rotated.Succeeded.Should().BeTrue();
        rotated.Tokens!.RefreshToken.Should().NotBe(original);

        // Replaying the already-rotated token is theft → rejected…
        var replay = await auth.RefreshAsync(original, null);
        replay.Succeeded.Should().BeFalse();
        replay.Error.Should().Be(AuthError.InvalidToken);

        // …and the descendant token must be dead too (family revocation).
        var descendant = await auth.RefreshAsync(rotated.Tokens.RefreshToken, null);
        descendant.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task OtpFlow_DeliversCode_VerifiesIt_AndRejectsWrongCodes()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await auth.RequestOtpAsync(AuthTestFixture.SchoolCode, AuthTestFixture.ParentPhone);
        _fixture.SmsSender.Sent.Should().NotBeEmpty("the OTP must be handed to the SMS gateway");

        var wrong = await auth.LoginWithOtpAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.ParentPhone, "000000", null);
        wrong.Succeeded.Should().BeFalse();
        wrong.Error.Should().Be(AuthError.InvalidOtp);

        var code = _fixture.SmsSender.LastCode();
        var right = await auth.LoginWithOtpAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.ParentPhone, code, null);
        right.Succeeded.Should().BeTrue();

        // Codes are single-use.
        var reuse = await auth.LoginWithOtpAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.ParentPhone, code, null);
        reuse.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task OtpRequest_ForUnknownPhone_IsSilentlyIgnored()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var sentBefore = _fixture.SmsSender.Sent.Count;
        await auth.RequestOtpAsync(AuthTestFixture.SchoolCode, "+910000000000");

        _fixture.SmsSender.Sent.Should().HaveCount(sentBefore,
            "unknown phones must not receive SMS nor produce a different response");
    }

    [Fact]
    public async Task Sessions_ListDevices_SurviveRotation_AndRevokeSignsOutThatDevice()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var login = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword,
            "10.0.0.9", deviceName: "Chrome · Windows");
        login.Succeeded.Should().BeTrue();
        var userId = UserIdOf(login);

        var sessions = await auth.GetSessionsAsync(userId);
        var session = sessions.Should()
            .Contain(s => s.DeviceName == "Chrome · Windows" && s.IpAddress == "10.0.0.9")
            .Subject;

        // Rotation keeps the device identity and the original sign-in time.
        var rotated = await auth.RefreshAsync(login.Tokens!.RefreshToken, "10.0.0.9");
        rotated.Succeeded.Should().BeTrue();
        var afterRotation = await auth.GetSessionsAsync(userId);
        var carried = afterRotation.Should()
            .ContainSingle(s => s.DeviceName == "Chrome · Windows")
            .Subject;
        carried.SignedInAt.Should().Be(session.SignedInAt);
        carried.Id.Should().NotBe(session.Id, "rotation replaces the token row");

        // Revoking the session kills that device's refresh chain…
        (await auth.RevokeSessionAsync(userId, carried.Id, "10.0.0.1")).Should().BeTrue();
        var refreshAfterRevoke = await auth.RefreshAsync(rotated.Tokens!.RefreshToken, "10.0.0.9");
        refreshAfterRevoke.Succeeded.Should().BeFalse();

        // …and it no longer appears in the list.
        (await auth.GetSessionsAsync(userId))
            .Should().NotContain(s => s.DeviceName == "Chrome · Windows");
    }

    [Fact]
    public async Task Sessions_CannotBeRevokedByAnotherUser()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var login = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword,
            null, deviceName: "Victim device");
        var userId = UserIdOf(login);
        var session = (await auth.GetSessionsAsync(userId))
            .Single(s => s.DeviceName == "Victim device");

        // A different user id (attacker) gets the same "false" as a miss.
        (await auth.RevokeSessionAsync(Guid.NewGuid(), session.Id, null)).Should().BeFalse();

        // The victim's session is untouched.
        (await auth.GetSessionsAsync(userId))
            .Should().Contain(s => s.Id == session.Id);
    }

    [Fact]
    public async Task Mfa_GatesLogin_AcceptsTotp_AndRecoveryCodesAreSingleUse()
    {
        await using var scope = _fixture.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var plain = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword, null);
        var userId = UserIdOf(plain);

        try
        {
            // Enroll and enable with a real authenticator code.
            var enrollment = await auth.StartMfaEnrollmentAsync(userId);
            enrollment!.SharedKey.Should().NotBeNullOrWhiteSpace();
            enrollment.AuthenticatorUri.Should().StartWith("otpauth://totp/SchoolErp:");

            (await auth.EnableMfaAsync(userId, "000000")).Should().BeNull("a wrong code must not enable MFA");

            var enabled = await auth.EnableMfaAsync(userId, Totp(enrollment.SharedKey));
            enabled.Should().NotBeNull();
            enabled!.RecoveryCodes.Should().HaveCount(8);

            // Password alone now yields a challenge, never tokens.
            var challenged = await auth.LoginWithPasswordAsync(
                AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword, null);
            challenged.Succeeded.Should().BeFalse();
            challenged.Error.Should().Be(AuthError.MfaRequired);
            challenged.Tokens.Should().BeNull();
            challenged.MfaChallenge.Should().NotBeNullOrWhiteSpace();

            // Wrong code fails; the right TOTP finishes the login.
            (await auth.CompleteMfaLoginAsync(challenged.MfaChallenge!, "123456", null))
                .Error.Should().Be(AuthError.InvalidMfaCode);
            var completed = await auth.CompleteMfaLoginAsync(
                challenged.MfaChallenge!, Totp(enrollment.SharedKey), null);
            completed.Succeeded.Should().BeTrue();
            completed.Tokens.Should().NotBeNull();

            // A recovery code works exactly once.
            var recovery = enabled.RecoveryCodes[0];
            var viaRecovery = await auth.CompleteMfaLoginAsync(
                (await LoginForChallengeAsync(auth)).MfaChallenge!, recovery, null);
            viaRecovery.Succeeded.Should().BeTrue();
            var reused = await auth.CompleteMfaLoginAsync(
                (await LoginForChallengeAsync(auth)).MfaChallenge!, recovery, null);
            reused.Succeeded.Should().BeFalse();

            // Disable (with a current code) restores plain login.
            (await auth.DisableMfaAsync(userId, Totp(enrollment.SharedKey))).Should().BeTrue();
        }
        finally
        {
            // Never leave the shared admin locked behind MFA for other tests.
            await ForceDisableMfaAsync(scope, userId);
        }

        var restored = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword, null);
        restored.Succeeded.Should().BeTrue();
    }

    private static async Task<AuthResult> LoginForChallengeAsync(IAuthService auth) =>
        await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, AuthTestFixture.AdminEmail, AuthTestFixture.AdminPassword, null);

    /// <summary>
    /// The code a real authenticator app would show right now — standard
    /// RFC 6238 TOTP (30s step, HMAC-SHA1, 6 digits) over the shared key.
    /// </summary>
    private static string Totp(string sharedKey)
    {
        var key = Base32Decode(sharedKey.Replace(" ", "", StringComparison.Ordinal));
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new System.Security.Cryptography.HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0xF;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) |
                     (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in input.TrimEnd('=').ToUpperInvariant())
        {
            value = (value << 5) | alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. output];
    }

    private static async Task ForceDisableMfaAsync(AsyncServiceScope scope, Guid userId)
    {
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<
                SchoolErp.Infrastructure.Identity.ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        await userManager.SetTwoFactorEnabledAsync(user!, false);
    }

    private static Guid UserIdOf(AuthResult result)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Tokens!.AccessToken);
        return Guid.Parse(jwt.Claims.First(c => c.Type is "sub" or "nameid").Value);
    }
}
