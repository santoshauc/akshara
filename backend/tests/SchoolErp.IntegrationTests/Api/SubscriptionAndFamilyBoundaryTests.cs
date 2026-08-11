using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// The two boundaries that are NOT permissions, and that a permission test would
/// therefore sail straight through.
///
/// The first is commercial: a school that has not bought the Library module must
/// be refused even when its administrator holds every library permission there
/// is. The second is the family guard, where the interesting property is which
/// refusal is used — answering 403 to a stranger asking about a child would
/// confirm that the child exists, so the parent endpoints deny knowledge with a
/// 404 instead.
///
/// Both live in filters and helpers that only run inside a real request, with
/// the tenant resolved from a real token.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SubscriptionAndFamilyBoundaryTests
{
    private readonly ApiFixture _api;

    public SubscriptionAndFamilyBoundaryTests(ApiFixture api) => _api = api;

    [Fact]
    public async Task A_module_the_school_has_not_bought_is_refused_despite_the_permission()
    {
        // This school's plan enables Core, Examination and Fees — not Library.
        // The principal is a full school admin, so library.view is present and
        // the ONLY thing that can produce a 403 here is the module gate.
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v1/library/books", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var title = body.RootElement.GetProperty("title").GetString();
        // A bare 403 would send an administrator hunting through role settings
        // for a permission problem that does not exist.
        title.Should().Contain("Library");
        title.Should().Contain("not enabled");
    }

    [Fact]
    public async Task A_module_the_school_does_have_is_let_through()
    {
        // The same principal against a module that IS on the plan. Without this
        // the previous test would still pass if the gate refused everything.
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(new Uri("/api/v1/exams/subjects", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_platform_request_is_never_module_gated()
    {
        // The gate reads the tenant's plan, and a platform account has no
        // tenant. It must skip rather than throw or refuse — an operator with no
        // school would otherwise be locked out of every gated controller.
        using var client = _api.CreateClient(TestPrincipal.PlatformOperator);

        var response = await client.GetAsync(new Uri("/api/v1/library/books", UriKind.Relative));

        // The tenant guard is what answers here, and it asks for a school rather
        // than refusing outright — which is the point: the module gate did not
        // fire first.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Select a school");
    }

    [Fact]
    public async Task A_parent_asking_about_someone_elses_child_is_told_it_does_not_exist()
    {
        // 404, deliberately, not 403. "Forbidden" would confirm the child is
        // enrolled here, which is exactly the fact being protected.
        using var client = _api.CreateClient(TestPrincipal.Parent);

        var response = await client.GetAsync(
            new Uri($"/api/v1/parent/children/{_api.UnrelatedStudentId}/fees", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_parent_with_no_children_gets_an_empty_list_rather_than_an_error()
    {
        using var client = _api.CreateClient(TestPrincipal.Parent);

        var response = await client.GetAsync(new Uri("/api/v1/parent/children", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_permissions_do_not_open_the_parent_endpoints()
    {
        // A school admin holds every tenant-assignable permission, and the parent
        // routes carry no permission requirement at all — only [Authorize]. What
        // keeps staff out of a family's data is the guard resolving the caller to
        // a guardian, so this must 404 for the same reason a stranger does.
        using var client = _api.CreateClient(TestPrincipal.SchoolAdmin);

        var response = await client.GetAsync(
            new Uri($"/api/v1/parent/children/{_api.UnrelatedStudentId}/fees", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
