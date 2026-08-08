using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Auth;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Users;
using SchoolErp.Infrastructure.Identity;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.IntegrationTests.Auth;

/// <summary>Staff/role administration and password lifecycle flows.</summary>
public sealed class UserAdminTests : IClassFixture<AuthTestFixture>
{
    private readonly AuthTestFixture _fixture;

    public UserAdminTests(AuthTestFixture fixture) => _fixture = fixture;

    private AsyncServiceScope CreateTenantScope()
    {
        var scope = _fixture.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>()
            .SetTenant(_fixture.TenantId);
        return scope;
    }

    [Fact]
    public async Task Custom_role_grants_exactly_its_permissions_at_login()
    {
        await using var scope = CreateTenantScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await users.CreateRoleAsync("Librarian", "Runs the library",
            [Permissions.Library.View, Permissions.Library.Manage]);
        (await users.GetRolesAsync())
            .Should().Contain(r => r.Name == "Librarian" && !r.IsSystem);

        await users.CreateUserAsync(
            "Leela Menon", "leela@demo.school", "+911234509876", "Temp@12345", ["Librarian"]);
        (await users.GetUsersAsync("leela"))
            .Should().ContainSingle(u => u.Roles.Contains("Librarian") && u.IsActive);

        var login = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, "leela@demo.school", "Temp@12345", null);
        login.Succeeded.Should().BeTrue();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.Tokens!.AccessToken);
        var permissions = jwt.Claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToList();
        permissions.Should().BeEquivalentTo([Permissions.Library.View, Permissions.Library.Manage],
            "a role must grant exactly its bundle — nothing more");
    }

    [Fact]
    public async Task Deactivation_blocks_login_and_kills_refresh_tokens()
    {
        await using var scope = CreateTenantScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await users.CreateRoleAsync("Clerk", null, [Permissions.Students.View]);
        var userId = await users.CreateUserAsync(
            "Deactivate Me", "bye@demo.school", null, "Temp@12345", ["Clerk"]);

        var login = await auth.LoginWithPasswordAsync(
            AuthTestFixture.SchoolCode, "bye@demo.school", "Temp@12345", null);
        login.Succeeded.Should().BeTrue();

        await users.UpdateUserAsync(userId, "Deactivate Me", isActive: false, ["Clerk"]);

        (await auth.LoginWithPasswordAsync(
                AuthTestFixture.SchoolCode, "bye@demo.school", "Temp@12345", null))
            .Error.Should().Be(AuthError.UserInactive);
        (await auth.RefreshAsync(login.Tokens!.RefreshToken, null))
            .Succeeded.Should().BeFalse("open sessions die on deactivation");
    }

    [Fact]
    public async Task Admin_reset_and_self_service_change_rotate_the_password()
    {
        await using var scope = CreateTenantScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await users.CreateRoleAsync("Accountant", null, [Permissions.Fees.View]);
        var userId = await users.CreateUserAsync(
            "Kiran Kumar", "kiran@demo.school", null, "First@12345", ["Accountant"]);

        // Admin reset: the old password stops working, the new one signs in.
        await users.ResetPasswordAsync(userId, "Second@12345");
        (await auth.LoginWithPasswordAsync(
                AuthTestFixture.SchoolCode, "kiran@demo.school", "First@12345", null))
            .Succeeded.Should().BeFalse();
        (await auth.LoginWithPasswordAsync(
                AuthTestFixture.SchoolCode, "kiran@demo.school", "Second@12345", null))
            .Succeeded.Should().BeTrue();

        // Self-service change requires the current password.
        (await auth.ChangePasswordAsync(userId, "WRONG@12345", "Third@12345"))
            .Should().NotBeNull();
        (await auth.ChangePasswordAsync(userId, "Second@12345", "Third@12345"))
            .Should().BeNull();
        (await auth.LoginWithPasswordAsync(
                AuthTestFixture.SchoolCode, "kiran@demo.school", "Third@12345", null))
            .Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Forgot_password_flows_through_a_phone_otp()
    {
        await using var scope = CreateTenantScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await users.CreateRoleAsync("FrontDesk", null, [Permissions.Students.View]);
        await users.CreateUserAsync(
            "Forgetful Staff", "forgot@demo.school", "+911234500001", "Old@12345", ["FrontDesk"]);

        await auth.RequestPasswordResetAsync(AuthTestFixture.SchoolCode, "forgot@demo.school");
        _fixture.SmsSender.Sent.Should().Contain(s => s.Phone == "+911234500001");
        var code = _fixture.SmsSender.LastCode();

        // Wrong code fails; the real one resets and the new password works.
        (await auth.ResetForgottenPasswordAsync(
                AuthTestFixture.SchoolCode, "forgot@demo.school", "000000", "New@12345"))
            .Should().BeFalse();
        (await auth.ResetForgottenPasswordAsync(
                AuthTestFixture.SchoolCode, "forgot@demo.school", code, "New@12345"))
            .Should().BeTrue();
        (await auth.LoginWithPasswordAsync(
                AuthTestFixture.SchoolCode, "forgot@demo.school", "New@12345", null))
            .Succeeded.Should().BeTrue();

        // Unknown logins stay silent — no SMS, no error.
        var sent = _fixture.SmsSender.Sent.Count;
        await auth.RequestPasswordResetAsync(AuthTestFixture.SchoolCode, "nobody@demo.school");
        _fixture.SmsSender.Sent.Should().HaveCount(sent);
    }

    [Fact]
    public async Task System_role_refuses_edits_and_unknown_roles_404()
    {
        await using var scope = CreateTenantScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var schoolAdmin = (await users.GetRolesAsync())
            .Single(r => r.Name == WellKnownRoles.SchoolAdmin);
        schoolAdmin.IsSystem.Should().BeTrue();
        var edit = () => users.UpdateRoleAsync(schoolAdmin.Id, "x", [Permissions.Fees.View]);
        await edit.Should().ThrowAsync<ConflictException>();

        var create = () => users.CreateUserAsync(
            "Ghost", "ghost@demo.school", null, "Temp@12345", ["NoSuchRole"]);
        await create.Should().ThrowAsync<NotFoundException>();
    }
}
