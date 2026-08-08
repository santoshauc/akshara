using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Api.Authorization;

/// <summary>
/// Declares which subscription module a controller (or action) belongs to.
/// Enforced by <see cref="ModuleGateFilter"/>; requests from tenants without
/// the module get a 403 with a clear message. Platform (Super Admin) requests
/// carry no tenant and are never gated.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresModuleAttribute : Attribute
{
    public RequiresModuleAttribute(TenantModules module) => Module = module;

    public TenantModules Module { get; }
}

/// <summary>Global action filter enforcing <see cref="RequiresModuleAttribute"/>.</summary>
public sealed class ModuleGateFilter : IAsyncActionFilter
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLookup _tenantLookup;

    public ModuleGateFilter(ITenantContext tenantContext, ITenantLookup tenantLookup)
    {
        _tenantContext = tenantContext;
        _tenantLookup = tenantLookup;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var required = context.ActionDescriptor.EndpointMetadata
            .OfType<RequiresModuleAttribute>()
            .FirstOrDefault();

        if (required is null || !_tenantContext.HasTenant)
        {
            await next();
            return;
        }

        var tenant = await _tenantLookup.FindByIdAsync(
            _tenantContext.TenantId, context.HttpContext.RequestAborted);
        if (tenant is not null && !tenant.EnabledModules.HasFlag(required.Module))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Title = $"The {required.Module} module is not enabled for your school. " +
                    "Contact the platform administrator to add it to your plan.",
                Status = StatusCodes.Status403Forbidden,
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
