using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Api.Authorization;

/// <summary>
/// Marks an authenticated endpoint that is deliberately NOT scoped to a school,
/// so <see cref="TenantGuardFilter"/> lets it through without one. The account
/// endpoints are the real case: a Super Admin must be able to change their own
/// password, manage MFA and revoke their own sessions, none of which belong to
/// a school.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoTenantRequiredAttribute : Attribute
{
}

/// <summary>
/// Refuses tenant-scoped requests that arrive with no school bound — which is
/// what a platform (Super Admin) token is.
/// <para>
/// Without this the two halves of the codebase failed differently and both
/// badly. Handlers that read <c>ITenantContext.TenantId</c> directly threw
/// deep inside an EF query and 500'd; handlers that leaned on the EF query
/// filter got its <c>Guid.Empty</c> fallback and answered 200 with an EMPTY
/// result — so a Super Admin was shown "0 students, ₹0 collected" as though
/// the school were empty. Silently wrong beats loudly broken for exactly
/// nobody, so both now become one clear 400.
/// </para>
/// <para>
/// The rule is opt-OUT on purpose. An endpoint that forgets to opt out fails
/// loudly the first time a platform account touches it; an opt-in scheme that
/// forgets to opt in goes back to serving empty data in production, which is
/// how this state of affairs arose.
/// </para>
/// </summary>
public sealed class TenantGuardFilter : IAsyncActionFilter
{
    private readonly ITenantContext _tenantContext;

    public TenantGuardFilter(ITenantContext tenantContext) => _tenantContext = tenantContext;

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_tenantContext.HasTenant || !RequiresTenant(context.ActionDescriptor.EndpointMetadata))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new ProblemDetails
        {
            Title = "Select a school before using this feature.",
            Detail = "This data belongs to one school, and the signed-in account is a " +
                "platform account with no school attached. Open the school from Schools, " +
                "or sign in with an account of that school.",
            Status = StatusCodes.Status400BadRequest,
        })
        { StatusCode = StatusCodes.Status400BadRequest };
    }

    private static bool RequiresTenant(IList<object> metadata)
    {
        // Anonymous endpoints (login, OTP, webhooks, public files) run before
        // anyone has a school.
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return false;
        }

        if (metadata.OfType<NoTenantRequiredAttribute>().Any())
        {
            return false;
        }

        // [PlatformOnly] demands the OPPOSITE — a principal with no tenant.
        // Checked before IAuthorizeData because it is an AuthorizeAttribute.
        if (metadata.OfType<PlatformOnlyAttribute>().Any())
        {
            return false;
        }

        // Everything else that demands a signed-in caller is school data.
        return metadata.OfType<IAuthorizeData>().Any();
    }
}
