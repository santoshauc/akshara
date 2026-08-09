using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Auth;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Platform;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.IntegrationTests.Auth;

/// <summary>
/// Operator accounts exist so the platform is not run from one shared login.
/// Reuses the auth fixture, which already seeds a platform user.
/// </summary>
public sealed class PlatformOperatorTests : IClassFixture<AuthTestFixture>
{
    private const string StrongPassword = "Operator@2026Pass";

    private readonly AuthTestFixture _fixture;

    public PlatformOperatorTests(AuthTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task An_operator_can_be_added_and_then_signs_in_as_a_platform_account()
    {
        await using var scope = _fixture.CreateScope();
        var operators = scope.ServiceProvider.GetRequiredService<IPlatformOperatorService>();

        var id = await operators.CreateOperatorAsync(
            "Second Operator", "ops2@schoolerp.local", StrongPassword);

        var listed = await operators.GetOperatorsAsync();
        listed.Should().Contain(o => o.Id == id && o.IsActive && !o.MfaEnabled);

        // No school code, and no school: the account is platform by construction.
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var login = await auth.LoginWithPasswordAsync(
            null, "ops2@schoolerp.local", StrongPassword, null);
        login.Succeeded.Should().BeTrue();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.SingleAsync(u => u.Id == id)).TenantId
            .Should().BeNull("an operator belongs to no school");
    }

    [Fact]
    public async Task A_duplicate_operator_email_is_refused()
    {
        await using var scope = _fixture.CreateScope();
        var operators = scope.ServiceProvider.GetRequiredService<IPlatformOperatorService>();

        await operators.CreateOperatorAsync(
            "Dupe One", "dupe@schoolerp.local", StrongPassword);

        var again = () => operators.CreateOperatorAsync(
            "Dupe Two", "DUPE@schoolerp.local", StrongPassword);
        await again.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Disabling_an_operator_blocks_their_login()
    {
        await using var scope = _fixture.CreateScope();
        var operators = scope.ServiceProvider.GetRequiredService<IPlatformOperatorService>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var id = await operators.CreateOperatorAsync(
            "Leaver", "leaver@schoolerp.local", StrongPassword);
        (await auth.LoginWithPasswordAsync(null, "leaver@schoolerp.local", StrongPassword, null))
            .Succeeded.Should().BeTrue();

        await operators.SetOperatorActiveAsync(id, false);

        var after = await auth.LoginWithPasswordAsync(
            null, "leaver@schoolerp.local", StrongPassword, null);
        after.Succeeded.Should().BeFalse();
        after.Error.Should().Be(AuthError.UserInactive);
    }

    [Fact]
    public async Task The_last_active_operator_cannot_be_disabled()
    {
        await using var scope = _fixture.CreateScope();
        var operators = scope.ServiceProvider.GetRequiredService<IPlatformOperatorService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Park every other operator so exactly one is left standing.
        var all = await operators.GetOperatorsAsync();
        var survivor = all.First(o => o.IsActive);
        foreach (var other in all.Where(o => o.IsActive && o.Id != survivor.Id))
        {
            await operators.SetOperatorActiveAsync(other.Id, false);
        }

        try
        {
            var act = () => operators.SetOperatorActiveAsync(survivor.Id, false);
            await act.Should().ThrowAsync<ConflictException>()
                .WithMessage("*last active operator*");
        }
        finally
        {
            // Shared fixture: put everyone back.
            foreach (var other in all.Where(o => o.IsActive))
            {
                var row = await db.Users.SingleAsync(u => u.Id == other.Id);
                row.IsActive = true;
            }

            await db.SaveChangesAsync();
        }
    }
}
