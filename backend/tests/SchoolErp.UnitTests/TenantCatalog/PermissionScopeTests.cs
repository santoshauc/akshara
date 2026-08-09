using FluentAssertions;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.UnitTests.TenantCatalog;

/// <summary>
/// The platform/tenant permission split. If these fail, a school role could
/// reach the school catalog or platform billing — cross-tenant escalation.
/// </summary>
public sealed class PermissionScopeTests
{
    [Fact]
    public void Platform_only_permissions_never_reach_school_roles()
    {
        Permissions.PlatformOnly.Should().Contain(Permissions.TenantCatalog.View)
            .And.Contain(Permissions.TenantCatalog.Manage);

        Permissions.TenantAssignable.Should().NotContain(Permissions.PlatformOnly);
        Permissions.TenantAssignable.Should().Contain(Permissions.Students.Manage,
            "school permissions must stay assignable");

        // The two sets partition the catalog exactly.
        Permissions.TenantAssignable.Concat(Permissions.PlatformOnly)
            .Should().BeEquivalentTo(Permissions.All);
    }
}
