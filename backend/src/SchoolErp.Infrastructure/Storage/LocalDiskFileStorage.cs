using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Storage;

/// <summary>
/// Local-disk <see cref="IFileStorage"/>. Root comes from
/// <c>Storage:RootPath</c> (default "data/files" under the content root, or
/// the current directory in tests). Keys are validated against a strict
/// pattern before ever touching the filesystem.
/// </summary>
public sealed partial class LocalDiskFileStorage : IFileStorage
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
    };

    private readonly string _root;
    private readonly ITenantContext _tenantContext;

    public LocalDiskFileStorage(IConfiguration configuration, ITenantContext tenantContext)
    {
        _root = Path.GetFullPath(configuration["Storage:RootPath"] ?? Path.Combine("data", "files"));
        _tenantContext = tenantContext;
    }

    public Task<string> SaveAsync(
        string category, string extension, Stream content, CancellationToken ct = default) =>
        SaveAsync(_tenantContext.TenantId, category, extension, content, ct);

    public async Task<string> SaveAsync(
        Guid tenantId, string category, string extension, Stream content,
        CancellationToken ct = default)
    {
        if (!CategoryPattern().IsMatch(category))
        {
            throw new ArgumentException("Category must be a short lowercase slug.", nameof(category));
        }

        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        if (!ContentTypes.ContainsKey(ext))
        {
            throw new ArgumentException($"Extension '{extension}' is not allowed.", nameof(extension));
        }

        var key = $"{tenantId:N}/{category}/{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct).ConfigureAwait(false);
        return key;
    }

    public Task<(Stream Content, string ContentType)?> OpenAsync(
        string key, CancellationToken ct = default)
    {
        var path = ResolveSafe(key);
        if (path is null || !File.Exists(path))
        {
            return Task.FromResult<(Stream, string)?>(null);
        }

        var contentType = ContentTypes.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
        Stream stream = File.OpenRead(path);
        return Task.FromResult<(Stream, string)?>((stream, contentType));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = ResolveSafe(key);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Null unless the key matches the generated shape exactly.</summary>
    private string? ResolveSafe(string key)
    {
        if (!KeyPattern().IsMatch(key))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        return path.StartsWith(_root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    [GeneratedRegex("^[a-z0-9-]{1,32}$")]
    private static partial Regex CategoryPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}/[a-z0-9-]{1,32}/[0-9a-f]{32}\.[a-z]{2,5}$")]
    private static partial Regex KeyPattern();
}
