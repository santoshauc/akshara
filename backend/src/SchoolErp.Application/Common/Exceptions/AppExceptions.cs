namespace SchoolErp.Application.Common.Exceptions;

/// <summary>Requested entity does not exist (or is invisible to the caller). Maps to 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.")
    {
    }
}

/// <summary>The operation conflicts with existing state (duplicates, stale versions). Maps to 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
