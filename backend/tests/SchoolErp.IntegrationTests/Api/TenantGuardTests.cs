using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>Tenant context that can be bound or left empty, as the middleware leaves it.</summary>
internal sealed class FakeTenantContext : ITenantContext
{
    private readonly Guid? _tenantId;

    public FakeTenantContext(Guid? tenantId) => _tenantId = tenantId;

    public Guid TenantId => _tenantId ?? throw new InvalidOperationException("No tenant bound.");

    public bool HasTenant => _tenantId.HasValue;
}

/// <summary>
/// A platform (Super Admin) token carries no school. Tenant-scoped endpoints
/// must say so plainly instead of throwing on TenantId or — worse — answering
/// 200 with empty data that reads as "this school has nothing in it".
/// </summary>
public sealed class TenantGuardTests
{
    private static readonly Guid SomeTenant = Guid.NewGuid();

    private static async Task<ActionExecutingContext> RunAsync(
        Guid? boundTenant, params object[] endpointMetadata)
    {
        var descriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata };
        var context = new ActionExecutingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor),
            [],
            new Dictionary<string, object?>(),
            controller: null!);

        var reached = false;
        await new TenantGuardFilter(new FakeTenantContext(boundTenant))
            .OnActionExecutionAsync(context, () =>
            {
                reached = true;
                return Task.FromResult(new ActionExecutedContext(context, [], controller: null!));
            });

        context.HttpContext.Items["reached"] = reached;
        return context;
    }

    private static bool Reached(ActionExecutingContext context) =>
        (bool)context.HttpContext.Items["reached"]!;

    [Fact]
    public async Task A_school_scoped_endpoint_without_a_school_is_refused_with_guidance()
    {
        var context = await RunAsync(null, new AuthorizeAttribute());

        Reached(context).Should().BeFalse("the handler must not run and answer with empty data");
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Contain("Select a school");
        problem.Detail.Should().NotBeNullOrWhiteSpace("the operator needs to know what to do next");
    }

    [Fact]
    public async Task The_same_endpoint_runs_normally_once_a_school_is_bound()
    {
        var context = await RunAsync(SomeTenant, new AuthorizeAttribute());

        Reached(context).Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task Anonymous_endpoints_run_without_a_school()
    {
        // Login, OTP, gateway webhooks and public files all happen before
        // anyone has a school.
        var context = await RunAsync(null, new AllowAnonymousAttribute());

        Reached(context).Should().BeTrue();
    }

    [Fact]
    public async Task Platform_only_endpoints_run_without_a_school()
    {
        // [PlatformOnly] demands the opposite of a tenant; gating it would
        // lock Super Admins out of Schools and Billing entirely.
        var context = await RunAsync(null, new PlatformOnlyAttribute());

        Reached(context).Should().BeTrue();
    }

    [Fact]
    public async Task Account_endpoints_opt_out_so_a_super_admin_keeps_mfa_and_sessions()
    {
        var context = await RunAsync(
            null, new AuthorizeAttribute(), new NoTenantRequiredAttribute());

        Reached(context).Should().BeTrue();
    }

    [Fact]
    public async Task An_endpoint_that_needs_no_sign_in_is_not_gated()
    {
        // No authorize metadata at all: nothing tenant-scoped to protect.
        var context = await RunAsync(null);

        Reached(context).Should().BeTrue();
    }
}
