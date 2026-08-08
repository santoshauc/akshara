using SchoolErp.Application.Abstractions;

namespace SchoolErp.IntegrationTests.Tenancy;

/// <summary>Bindable tenant context for tests; mirrors the production semantics.</summary>
internal sealed class StubTenantContext : ITenantContext, ITenantContextSetter
{
    private Guid? _tenantId;

    public StubTenantContext(Guid? tenantId = null) => _tenantId = tenantId;

    public Guid TenantId => _tenantId
        ?? throw new InvalidOperationException("No tenant bound to this test scope.");

    public bool HasTenant => _tenantId.HasValue;

    public void SetTenant(Guid tenantId) => _tenantId = tenantId;
}

/// <summary>Fixed test user for audit stamping.</summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public string? UserId => "test-user";

    public string? UserName => "Integration Test";

    public bool IsAuthenticated => true;
}

/// <summary>
/// Test user with a REAL Guid id, for flows that parse it (leave requests).
/// Settable so one fixture can act as different people per scope.
/// </summary>
internal sealed class GuidCurrentUser : ICurrentUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? UserId => Id.ToString();

    public string? UserName => "Integration Test";

    public bool IsAuthenticated => true;
}
