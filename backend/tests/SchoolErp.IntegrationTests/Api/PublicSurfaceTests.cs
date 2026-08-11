using System.Net;
using System.Text;
using FluentAssertions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// The endpoints anyone on the internet can reach without a token.
///
/// These carry the most risk per line in the whole API and had no test at all:
/// the gateway webhook, whose only proof of authenticity is an HMAC signature;
/// the file route, which is anonymous by design and takes a catch-all path
/// segment straight from the URL; and the two things that must NOT be mounted
/// outside Development. Every one of them is a filter-free path that only exists
/// once the app is actually listening.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class PublicSurfaceTests
{
    private readonly ApiFixture _api;

    public PublicSurfaceTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task A_payment_webhook_with_no_signature_is_refused()
    {
        // Anonymous endpoint that moves money into a school's ledger. An
        // unsigned body must never be acted on.
        using var client = _api.CreateAnonymousClient();

        var response = await client.PostAsync(
            new Uri("/api/v1/payments/webhook", UriKind.Relative),
            new StringContent("""{"event":"payment.captured"}""", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_payment_webhook_with_a_forged_signature_is_refused()
    {
        using var client = _api.CreateAnonymousClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri("/api/v1/payments/webhook", UriKind.Relative))
        {
            Content = new StringContent(
                """{"event":"payment.captured","amount":50000}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Webhook-Signature", "0000000000000000000000000000000000000000000000000000000000000000");

        var response = await client.SendAsync(request);

        // Rejected on the signature, before anything is touched - the comment on
        // the controller says authenticity comes from the HMAC, and this is what
        // holds that claim to account.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_file_key_that_climbs_out_of_the_store_gets_nothing()
    {
        // The route is anonymous and its key is a catch-all taken from the URL,
        // so traversal is the obvious attack. Storage validates the key shape and
        // re-checks the resolved path stays under the root; this proves the whole
        // chain refuses rather than serving a file from the application directory.
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(
            new Uri("/api/v1/files/..%2F..%2F..%2Fappsettings.json", UriKind.Relative));

        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().NotContain("ConnectionStrings", "configuration must never be served as a file");
    }

    [Fact]
    public async Task An_unknown_file_key_is_a_404_rather_than_a_server_error()
    {
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/files/{Guid.NewGuid():N}/photo/{Guid.NewGuid():N}.jpg", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_root_identifies_the_service_instead_of_redirecting_to_docs()
    {
        // In Development "/" redirects to Swagger. Anywhere else it must answer
        // with plain service info, because there is no UI here and a redirect to
        // an unmounted route would be a confusing dead end.
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SchoolErp API");
    }

    [Fact]
    public async Task Swagger_is_not_mounted_outside_development()
    {
        // Publishing the full API surface, including every platform endpoint, to
        // anonymous callers is not something to leave to chance.
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
