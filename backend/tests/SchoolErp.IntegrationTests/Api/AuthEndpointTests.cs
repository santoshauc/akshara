using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// Signing in for real, over HTTP, and the brute-force budget that protects it.
///
/// This class joins the shared collection but takes its OWN app host. The
/// distinction matters: the collection gives it the one container and the one
/// seeded school, while the private host gives it a private rate-limiter budget,
/// so a test that deliberately exhausts that budget cannot hand every other test
/// an unrelated 429. Taking its own FIXTURE instead would start a second
/// container and, worse, re-point the process-wide connection environment out
/// from under the shared host.
///
/// Everything else in these files mints its token in-process, which keeps the
/// suite off that budget but means nothing else proves the login endpoint issues
/// a token the API will actually accept. That round trip is what this file is
/// for.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class AuthEndpointTests : IDisposable
{
    private readonly ApiFactory _host;

    public AuthEndpointTests(ApiFixture api) => _host = api.CreateIsolatedHost();

    public void Dispose() => _host.Dispose();

    private HttpClient CreateClient() => _host.CreateClient();

    [Fact]
    public async Task A_correct_password_returns_a_token_the_api_accepts()
    {
        using var client = CreateClient();

        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new
            {
                schoolCode = ApiFixture.SchoolCode,
                login = ApiFixture.SchoolAdminEmail,
                password = ApiFixture.SchoolAdminPassword,
            });

        login.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = body.RootElement.GetProperty("accessToken").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();

        // The half that matters. A token the login endpoint issues but the
        // authentication middleware rejects — mismatched issuer, audience or
        // signing key — is a working login and a broken product, and only a
        // second request can tell the difference.
        using var authenticated = CreateClient();
        authenticated.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await authenticated.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_wrong_password_is_refused_without_saying_which_half_was_wrong()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new
            {
                schoolCode = ApiFixture.SchoolCode,
                login = ApiFixture.SchoolAdminEmail,
                password = "definitely-not-the-password",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Naming the wrong half turns a login form into an account enumerator.
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContainEquivalentOf("password is incorrect");
        text.Should().NotContainEquivalentOf("no such user");
    }

    [Fact]
    public async Task Credential_stuffing_runs_out_of_budget_before_it_runs_out_of_guesses()
    {
        // The login endpoint carries a tighter limiter than the rest of the API
        // precisely so a password can't be guessed at network speed. The
        // identity used here does not exist, so nothing real gets locked out
        // and the only thing under test is the limiter.
        using var client = CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 14; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                new Uri("/api/v1/auth/login", UriKind.Relative),
                new
                {
                    schoolCode = ApiFixture.SchoolCode,
                    login = $"nobody-{attempt}@http.test",
                    password = "guess",
                });
            statuses.Add(response.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests,
            "the credential endpoints are limited to ten attempts a minute");

        // The early attempts must still have been answered normally — a limiter
        // that refuses from the first request would be a broken login page.
        statuses[0].Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Requesting_an_otp_says_nothing_about_whether_the_number_is_known()
    {
        // A registered and an unregistered phone have to be INDISTINGUISHABLE
        // from outside, or the endpoint becomes a way to ask whether a given
        // family attends the school. Testing only the unknown number would miss
        // exactly the bug worth guarding against, so both are compared.
        using var client = CreateClient();

        var known = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/otp/request", UriKind.Relative),
            new { schoolCode = ApiFixture.SchoolCode, phone = ApiFixture.ParentPhone });

        var unknown = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/otp/request", UriKind.Relative),
            new { schoolCode = ApiFixture.SchoolCode, phone = "+919000099999" });

        known.StatusCode.Should().Be(HttpStatusCode.Accepted);
        unknown.StatusCode.Should().Be(known.StatusCode);
        (await unknown.Content.ReadAsStringAsync())
            .Should().Be(await known.Content.ReadAsStringAsync());
    }
}
