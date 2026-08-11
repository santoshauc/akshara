using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// What the API actually puts on the wire: headers, problem bodies, routing and
/// the health probes.
///
/// These are the parts nothing else can check. The middleware that adds the
/// security headers is a lambda in Program.cs with no type to instantiate; the
/// api-version constraint only exists once routing runs; and the readiness split
/// is a property of how the two health endpoints are mapped, not of any class.
/// ExceptionMappingTests proves the exception FILTER builds the right object —
/// it cannot prove that object survives serialization, which is precisely where
/// this codebase has already lost per-field validation errors once.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PipelineContractTests
{
    private readonly ApiFixture _api;

    public PipelineContractTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task Every_response_carries_the_security_header_baseline()
    {
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
    }

    [Fact]
    public async Task The_security_headers_are_present_on_failures_too()
    {
        // The header middleware sits ahead of everything that can fail, so a
        // 401 must be as hardened as a 200. If it ever moves below the auth
        // middleware this is the test that notices.
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/students", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Fact]
    public async Task A_rejected_command_still_names_the_fields_that_were_wrong()
    {
        // THE REGRESSION GUARD. Validation responses once reached clients with
        // their per-field errors stripped: the switch expression typed the value
        // as ProblemDetails and WriteAsJsonAsync serializes by the DECLARED
        // type, so ValidationProblemDetails.Errors never went out. The filter
        // was building the right object the whole time, which is why only a
        // test that reads the wire can catch it.
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/students", UriKind.Relative),
            new
            {
                firstName = string.Empty,
                lastName = string.Empty,
                dateOfBirth = "2015-01-01",
                gender = 1,
                admissionDate = "2026-04-01",
                academicYearId = Guid.Empty,
                schoolClassId = Guid.Empty,
                sectionId = Guid.Empty,
                guardians = Array.Empty<object>(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("errors", out var errors)
            .Should().BeTrue("a validation failure has to say WHICH fields failed");
        errors.EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_missing_record_answers_a_problem_document_not_an_empty_body()
    {
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(
            new Uri($"/api/v1/students/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("title").GetString().Should().Contain("Student");
        body.RootElement.GetProperty("status").GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task An_unknown_route_is_a_plain_404()
    {
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v1/not-a-real-endpoint", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_version_nobody_serves_does_not_fall_through_to_v1()
    {
        // Recorded because it is easy to assume otherwise: nothing is registered
        // at v2, so the apiVersion route constraint never matches and the
        // request dies in ROUTING as a 404 — Asp.Versioning never gets to answer
        // "unsupported version" with a 400. What matters either way is that a
        // client asking for a version this build does not have is refused
        // rather than quietly served v1 data. If v2 endpoints are ever added,
        // expect this to become a 400 for the endpoints that skip v2.
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v2/students", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Liveness_stays_up_while_readiness_reports_the_broken_dependency()
    {
        // The split is the entire reason there are two probes: a Redis outage
        // must stop traffic being routed here, NOT get the process restarted by
        // the orchestrator. This host is pointed at a dead Redis port on purpose.
        using var client = _api.CreateAnonymousClient();

        var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        live.StatusCode.Should().Be(HttpStatusCode.OK, "liveness must not depend on anything downstream");
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task A_browser_on_the_configured_origin_is_allowed_through_cors()
    {
        using var client = _api.CreateAnonymousClient();

        using var preflight = new HttpRequestMessage(
            HttpMethod.Options, new Uri("/api/v1/students", UriKind.Relative));
        preflight.Headers.Add("Origin", ApiFixture.AllowedOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(preflight);

        response.Headers.GetValues("Access-Control-Allow-Origin")
            .Should().ContainSingle().Which.Should().Be(ApiFixture.AllowedOrigin);
    }

    [Fact]
    public async Task An_origin_nobody_configured_gets_no_cors_grant()
    {
        using var client = _api.CreateAnonymousClient();

        using var preflight = new HttpRequestMessage(
            HttpMethod.Options, new Uri("/api/v1/students", UriKind.Relative));
        preflight.Headers.Add("Origin", "https://evil.example");
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(preflight);

        // Absence is the mechanism — the browser refuses the response because
        // the grant is missing, not because the server said no.
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
