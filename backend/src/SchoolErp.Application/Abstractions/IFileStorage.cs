namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Binary file storage. Keys are server-generated
/// ("{tenantId}/{category}/{guid}{ext}") — user-supplied names never touch
/// the underlying store, which kills path-traversal by construction.
/// Local disk today; the same contract fits S3-style storage later.
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores the content under a fresh tenant-scoped key and returns it.</summary>
    Task<string> SaveAsync(
        string category, string extension, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Same, but under an explicit tenant — for platform operations (e.g. a
    /// Super Admin uploading a school's logo) where the ambient tenant
    /// context is not the target school.
    /// </summary>
    Task<string> SaveAsync(
        Guid tenantId, string category, string extension, Stream content,
        CancellationToken ct = default);

    /// <summary>Opens a stored file, or null when the key is unknown/invalid.</summary>
    Task<(Stream Content, string ContentType)?> OpenAsync(string key, CancellationToken ct = default);

    /// <summary>Deletes a stored file; unknown keys are ignored.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
