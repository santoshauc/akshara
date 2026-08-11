using System.Net;
using FluentAssertions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// Authorization as the client experiences it: a real request, a real token, a
/// real status code.
///
/// The class of bug this catches is the one this product has already shipped
/// once. School admin roles were backfilled with every permission including
/// tenants.manage, and the platform endpoints checked only the permission claim
/// — so a school administrator could list and edit every school on the platform
/// and grant themselves SMS credits. The fix was to gate those endpoints on a
/// POLICY that demands the absence of a tenant claim, not on a permission. A
/// test that instantiates a filter cannot see whether that policy is actually
/// wired to the endpoint; only a request can.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationPipelineTests
{
    private readonly ApiFixture _api;

    public AuthorizationPipelineTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task An_unauthenticated_request_is_challenged_rather_than_forbidden()
    {
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        // 401 and 403 are different instructions to the caller: sign in, versus
        // you are signed in and still may not. The portal branches on exactly
        // this to decide between the login page and an error.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_token_carrying_the_permission_reaches_the_handler()
    {
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_token_without_the_permission_is_forbidden_not_challenged()
    {
        // This principal holds attendance permissions and nothing else, which is
        // what a classroom teacher's bundle looks like.
        using var client = _api.CreateClient(TestPrincipal.LimitedStaff);

        var response = await client.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_school_token_cannot_reach_the_tenant_catalog_even_holding_the_permission()
    {
        // The regression guard. This principal carries tenants.view AND
        // tenants.manage. If platform endpoints ever go back to checking the
        // permission rather than the policy, this is a 200 and a school admin
        // owns the platform again.
        using var client = _api.CreateClient(TestPrincipal.SchoolAdminWithPlatformPermission);

        var response = await client.GetAsync(new Uri("/api/v1/tenants", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_platform_operator_with_mfa_reaches_the_tenant_catalog()
    {
        using var client = _api.CreateClient(TestPrincipal.PlatformOperator);

        var response = await client.GetAsync(new Uri("/api/v1/tenants", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_platform_operator_who_has_not_enrolled_mfa_is_refused()
    {
        // An operator can edit every school, so signing in without a second
        // factor must buy them nothing but the Security page on which to enable
        // it. The token carries every permission there is — the MFA claim alone
        // is what stops it.
        using var client = _api.CreateClient(TestPrincipal.PlatformOperatorWithoutMfa);

        var response = await client.GetAsync(new Uri("/api/v1/tenants", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_platform_token_on_a_school_scoped_endpoint_is_told_to_pick_a_school()
    {
        // A Super Admin has no school. Without the tenant guard the handler
        // either throws on TenantId or — worse — answers 200 with an empty list,
        // which reads as "this school has no students".
        using var client = _api.CreateClient(TestPrincipal.PlatformOperator);

        var response = await client.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadAsStringAsync();
        problem.Should().Contain("Select a school");
    }

    [Fact]
    public async Task An_anonymous_endpoint_on_a_platform_only_controller_still_answers()
    {
        // Branding sits on the platform-only tenants controller but must serve
        // login screens before anyone has signed in. [AllowAnonymous] has to win
        // over both [PlatformOnly] and the tenant guard, which is a three-way
        // interaction no filter test can reproduce.
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/tenants/branding?code={ApiFixture.SchoolCode}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_garbled_token_is_challenged_rather_than_crashing_the_pipeline()
    {
        using var client = _api.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
