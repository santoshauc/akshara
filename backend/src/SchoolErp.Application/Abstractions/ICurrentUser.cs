namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Identity of the caller for the current scope. Backed by the JWT in HTTP
/// requests and by explicit job metadata in background jobs.
/// </summary>
public interface ICurrentUser
{
    /// <summary>User id, or null for anonymous/system scopes.</summary>
    string? UserId { get; }

    /// <summary>Display name for audit trails.</summary>
    string? UserName { get; }

    bool IsAuthenticated { get; }
}
