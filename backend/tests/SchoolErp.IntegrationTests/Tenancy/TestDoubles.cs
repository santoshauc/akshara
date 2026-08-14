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

/// <summary>Records email instead of sending it.</summary>
public sealed class RecordingEmailSender : SchoolErp.Application.Abstractions.IEmailSender
{
    private readonly List<(string To, string Subject, string Body)> _sent = [];

    public IReadOnlyList<(string To, string Subject, string Body)> Sent => _sent;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        _sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>Records push notifications instead of sending them.</summary>
public sealed class RecordingPushSender : SchoolErp.Application.Notifications.IPushSender
{
    private readonly List<(string Token, string Title, string Body)> _sent = [];

    public IReadOnlyList<(string Token, string Title, string Body)> Sent => _sent;

    public Task SendAsync(string token, string title, string body, CancellationToken ct = default)
    {
        _sent.Add((token, title, body));
        return Task.CompletedTask;
    }
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
