using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// An audit of the ENTIRE routing table, rather than of endpoints someone
/// remembered to write a test for.
///
/// This API has no fallback authorization policy: AddAuthorization registers
/// only the platform-only policy, so an action with no [HasPermission] and no
/// [Authorize] is served to anybody who asks. Nothing about that is visible when
/// reading a single controller — the omission looks exactly like the code you
/// meant to write. These tests enumerate every registered endpoint from the
/// running host, so a controller added next year is covered the day it is
/// mapped, with no list to maintain.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class EndpointExposureTests
{
    private readonly ApiFixture _api;

    public EndpointExposureTests(ApiFixture api) => _api = api;

    /// <summary>
    /// Routes that carry NO authorization metadata and NO explicit
    /// [AllowAnonymous] — nobody stated an intention either way.
    ///
    /// An endpoint marked [AllowAnonymous] is not listed here: that attribute is
    /// itself the decision, recorded in the place a reviewer reads. What this
    /// list holds is the residue — endpoints mapped outside MVC, where there is
    /// no attribute to write. Anything else arriving here is an omission.
    /// </summary>
    private static readonly Dictionary<string, string> UnannotatedByDesign = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = "Service identification; there is no UI at the API root.",
        ["/health/live"] = "Liveness probe; must answer before anything else does.",
        ["/health/ready"] = "Readiness probe, same.",
    };

    private IReadOnlyList<RouteEndpoint> Endpoints() =>
        [.. _api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()];

    [Fact]
    public void No_endpoint_is_left_without_a_stated_intention()
    {
        // The audit. An action missing its [HasPermission] has neither
        // authorization nor an [AllowAnonymous] saying that was on purpose, so
        // it surfaces here as a named route rather than as a hole someone else
        // finds later.
        var undeclared = Endpoints()
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(e => e.RoutePattern.RawText ?? "(no pattern)")
            .Where(pattern => !UnannotatedByDesign.ContainsKey(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pattern => pattern, StringComparer.Ordinal)
            .ToList();

        undeclared.Should().BeEmpty(
            "an endpoint with neither [Authorize]/[HasPermission] nor [AllowAnonymous] is served " +
            "to anonymous callers, and this API has no fallback policy to catch the omission");
    }

    [Fact]
    public void The_unannotated_exceptions_all_still_exist()
    {
        // Keeps the list honest in the other direction: a stale entry would
        // quietly excuse a route that no longer resolves to what it names, and
        // this list is the only record of why each one carries no attribute.
        var patterns = Endpoints()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = UnannotatedByDesign.Keys.Where(k => !patterns.Contains(k)).ToList();

        stale.Should().BeEmpty("the list must describe routes that actually exist");
    }

    [Fact]
    public void Anonymous_access_is_only_ever_granted_deliberately()
    {
        // Every open route, named, with the reason it is open recorded in the
        // test that asserts it. If this list grows, someone widened the public
        // surface of the product and a reviewer gets to see it in the diff.
        var expected = new[]
        {
            "api/v{version:apiVersion}/admissions/enquiries/public",
            "api/v{version:apiVersion}/auth/login",
            "api/v{version:apiVersion}/auth/logout",
            "api/v{version:apiVersion}/auth/mfa/verify",
            "api/v{version:apiVersion}/auth/otp/request",
            "api/v{version:apiVersion}/auth/otp/verify",
            "api/v{version:apiVersion}/auth/password/forgot",
            "api/v{version:apiVersion}/auth/password/reset",
            "api/v{version:apiVersion}/auth/refresh",
            "api/v{version:apiVersion}/files/{**key}",
            "api/v{version:apiVersion}/payments/checkout/{gatewayOrderId}",
            "api/v{version:apiVersion}/payments/checkout/{gatewayOrderId}/dev-complete",
            "api/v{version:apiVersion}/payments/webhook",
            "api/v{version:apiVersion}/tenants/branding",
        };

        var actual = Endpoints()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        actual.Should().BeEquivalentTo(expected,
            "the anonymous surface is the product's attack surface; it should never change silently");
    }

    [Fact]
    public async Task Every_protected_endpoint_actually_challenges_an_anonymous_caller()
    {
        // Declaring [Authorize] and ENFORCING it are different claims. This
        // issues a real request to each protected route with no credentials and
        // insists on a 401 - catching a filter ordering or middleware change
        // that lets requests through before authorization runs.
        using var client = _api.CreateAnonymousClient();
        var leaked = new List<string>();

        foreach (var endpoint in Endpoints())
        {
            if (endpoint.Metadata.GetMetadata<IAuthorizeData>() is null) continue;
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null) continue;

            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? "GET";
            var url = Concretize(endpoint.RoutePattern.RawText ?? string.Empty);

            using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative));
            var response = await client.SendAsync(request);

            // Authorization runs before model binding, so a missing body cannot
            // turn a 401 into a 400. 429 is accepted alongside 401 for one
            // reason: sweeping the credential endpoints spends their deliberately
            // tight 10-a-minute budget, and a request refused by the limiter is
            // just as firmly refused before reaching a handler. Anything else
            // means the request got further than it should have.
            if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests))
            {
                leaked.Add($"{method} {url} -> {(int)response.StatusCode}");
            }
        }

        leaked.Should().BeEmpty("these endpoints declare authorization but did not challenge an anonymous caller");
    }

    /// <summary>
    /// Turns a route template into something requestable. Values are deliberately
    /// arbitrary: the request must never get far enough for them to matter.
    /// </summary>
    private static string Concretize(string pattern)
    {
        var url = pattern
            .Replace("{version:apiVersion}", "1", StringComparison.Ordinal)
            .Replace("{**key}", "missing/file.png", StringComparison.Ordinal);

        // Any remaining {param}, {param:guid}, {param?} placeholder.
        url = Regex.Replace(url, @"\{[^}]+\}", match =>
            match.Value.Contains(":guid", StringComparison.Ordinal)
                ? Guid.Empty.ToString()
                : "x");

        return url.StartsWith('/') ? url : "/" + url;
    }
}
