using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SchoolErp.Infrastructure.Identity;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Authorization;

/// <summary>
/// Declares that an endpoint requires a permission from
/// <see cref="Permissions"/>. Never check role names on endpoints — roles are
/// tenant-editable bundles of permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public HasPermissionAttribute(string permission)
        : base(PolicyPrefix + permission)
    {
    }
}

/// <summary>Requirement carrying the demanded permission value.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}

/// <summary>
/// Restricts an endpoint to PLATFORM principals — tokens with no tenant
/// claim (Super Admin and future platform staff). School tokens are refused
/// even when they somehow carry the demanded permission: permission claims
/// are tenant-editable data, platform scope is not.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PlatformOnlyAttribute : AuthorizeAttribute
{
    public const string PolicyName = "platform-only";

    public PlatformOnlyAttribute()
        : base(PolicyName)
    {
    }
}

/// <summary>
/// Grants access when the token carries the demanded permission claim, or the
/// caller is a platform SuperAdmin (who implicitly holds every permission).
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var granted =
            context.User.IsInRole(WellKnownRoles.SuperAdmin) ||
            context.User.HasClaim(Permissions.ClaimType, requirement.Permission);

        if (granted)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Materializes <c>perm:*</c> policies on demand so new permissions never need
/// central policy registration.
/// </summary>
public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
        }

        return await base.GetPolicyAsync(policyName).ConfigureAwait(false);
    }
}
