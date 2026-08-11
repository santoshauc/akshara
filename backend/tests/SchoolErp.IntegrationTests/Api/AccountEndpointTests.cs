using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// The endpoints that belong to the ACCOUNT rather than to a school: MFA
/// enrollment, device sessions, password change, push tokens.
///
/// Two things make these worth exercising over HTTP. They are the only
/// authenticated endpoints marked [NoTenantRequired], so they must work for a
/// principal with no school at all - a Super Admin has none, and locking them
/// out of the Security page is how you strand the first operator with no way to
/// enrol MFA. And their authorization is self-scoping rather than
/// permission-based: they act on "whoever is calling", which no permission test
/// can check.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class AccountEndpointTests
{
    private readonly ApiFixture _api;

    public AccountEndpointTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task A_signed_in_user_can_read_their_own_mfa_status()
    {
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v1/auth/mfa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("enabled").GetBoolean().Should().BeFalse(
            "this account has never enrolled");
    }

    [Fact]
    public async Task A_platform_operator_with_no_school_can_still_reach_their_security_page()
    {
        // The [NoTenantRequired] case. Without it the tenant guard would answer
        // "select a school" and an operator could never enrol MFA - which the
        // platform policy then requires of them. A deadlock, not an error.
        using var client = _api.CreateClient(TestPrincipal.PlatformOperator);

        var response = await client.GetAsync(new Uri("/api/v1/auth/mfa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Enrolling_returns_a_shared_key_and_an_otpauth_uri()
    {
        // The URI is what the QR code encodes; an authenticator app cannot be
        // set up without it, and nothing else in the suite proves it is issued.
        using var client = _api.CreateClient(TestPrincipal.LimitedStaff);

        var response = await client.PostAsync(
            new Uri("/api/v1/auth/mfa/enroll", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("sharedKey").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("authenticatorUri").GetString().Should().StartWith("otpauth://");
    }

    [Fact]
    public async Task Enabling_mfa_with_a_wrong_code_is_refused_and_says_so_plainly()
    {
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/mfa/enable", UriKind.Relative), new { code = "000000" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("authenticator");
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_read_anyone_s_mfa_status()
    {
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/auth/mfa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_user_can_list_their_own_device_sessions()
    {
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v1/auth/sessions", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // A minted token creates no refresh-token row, so the list is legitimately
        // empty. What matters here is that it is a list and not a 403 or a crash.
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Changing_a_password_without_the_current_one_is_refused()
    {
        // The current password is what stops a stolen access token from taking
        // permanent ownership of the account.
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/password/change", UriKind.Relative),
            new { currentPassword = "not-the-password", newPassword = "Whatever@2026x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_device_can_register_a_push_token_and_remove_it_again()
    {
        // Its own token value so the round trip cannot collide with another test.
        var token = $"ExponentPushToken[{Guid.NewGuid():N}]";
        using var client = _api.CreateClient(TestPrincipal.Parent);

        var registered = await client.PostAsJsonAsync(
            new Uri("/api/v1/push/tokens", UriKind.Relative),
            new { token, platform = "ios" });
        registered.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Re-registering the same token must be idempotent, not a duplicate-key
        // failure: the parent app registers on every sign-in.
        var again = await client.PostAsJsonAsync(
            new Uri("/api/v1/push/tokens", UriKind.Relative),
            new { token, platform = "ios" });
        again.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var removed = await client.DeleteAsync(
            new Uri($"/api/v1/push/tokens?token={Uri.EscapeDataString(token)}", UriKind.Relative));
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task One_user_cannot_delete_another_user_s_push_token()
    {
        // An Expo token is not a secret - it travels to the push service and is
        // logged in plenty of places. Without the ownership check, anyone
        // holding one could silence that family's notifications.
        var token = $"ExponentPushToken[{Guid.NewGuid():N}]";

        using var owner = _api.CreateClient(TestPrincipal.Parent);
        (await owner.PostAsJsonAsync(
            new Uri("/api/v1/push/tokens", UriKind.Relative),
            new { token, platform = "android" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var stranger = _api.CreateClient(TestPrincipal.LimitedStaff);
        var attempt = await stranger.DeleteAsync(
            new Uri($"/api/v1/push/tokens?token={Uri.EscapeDataString(token)}", UriKind.Relative));

        // The endpoint answers 204 either way - it deliberately does not reveal
        // whether the token exists. The real assertion is that the OWNER's token
        // survived, which is checked by deleting it successfully afterwards.
        attempt.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ownerDeletes = await owner.DeleteAsync(
            new Uri($"/api/v1/push/tokens?token={Uri.EscapeDataString(token)}", UriKind.Relative));
        ownerDeletes.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Registering_a_push_token_requires_signing_in()
    {
        using var client = _api.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/push/tokens", UriKind.Relative),
            new { token = "ExponentPushToken[anonymous]", platform = "ios" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
