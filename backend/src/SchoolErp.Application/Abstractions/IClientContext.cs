namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Transport-level facts about the caller (for audit trails). HTTP requests
/// supply the remote IP; background jobs and tests have none.
/// </summary>
public interface IClientContext
{
    string? IpAddress { get; }
}

/// <summary>Default for non-HTTP scopes (jobs, tests).</summary>
public sealed class NullClientContext : IClientContext
{
    public string? IpAddress => null;
}
