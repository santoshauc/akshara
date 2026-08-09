using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Storage;

/// <summary>
/// S3-compatible <see cref="IFileStorage"/> — AWS S3, Cloudflare R2, MinIO,
/// or any S3 API. Selected with <c>Storage:Provider=s3</c>; configuration:
/// <c>Storage:S3:Bucket</c> (required), <c>Storage:S3:Region</c> or
/// <c>Storage:S3:ServiceUrl</c> (for non-AWS endpoints), and
/// <c>Storage:S3:AccessKey/SecretKey</c> (falls back to the AWS default
/// credential chain — instance profiles, env vars — when omitted).
/// Keys keep the exact same shape as local storage, so switching providers
/// never breaks stored URLs.
/// </summary>
public sealed partial class S3FileStorage : IFileStorage, IDisposable
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
    };

    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly ITenantContext _tenantContext;

    public S3FileStorage(IConfiguration configuration, ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
        _bucket = configuration["Storage:S3:Bucket"]
            ?? throw new InvalidOperationException(
                "Storage:S3:Bucket is required when Storage:Provider is 's3'.");

        var config = new AmazonS3Config();
        if (configuration["Storage:S3:ServiceUrl"] is { Length: > 0 } serviceUrl)
        {
            // Non-AWS S3 (MinIO, R2) — path style avoids virtual-host DNS needs.
            config.ServiceURL = serviceUrl;
            config.ForcePathStyle = true;
        }
        else if (configuration["Storage:S3:Region"] is { Length: > 0 } region)
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        }

        var accessKey = configuration["Storage:S3:AccessKey"];
        var secretKey = configuration["Storage:S3:SecretKey"];
        _client = string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey)
            ? new AmazonS3Client(config) // default AWS credential chain
            : new AmazonS3Client(accessKey, secretKey, config);
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
        if (!ContentTypes.TryGetValue(ext, out var contentType))
        {
            throw new ArgumentException($"Extension '{extension}' is not allowed.", nameof(extension));
        }

        var key = $"{tenantId:N}/{category}/{Guid.NewGuid():N}{ext}";
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
        }, ct).ConfigureAwait(false);
        return key;
    }

    public async Task<(Stream Content, string ContentType)?> OpenAsync(
        string key, CancellationToken ct = default)
    {
        if (!KeyPattern().IsMatch(key))
        {
            return null;
        }

        try
        {
            var response = await _client.GetObjectAsync(_bucket, key, ct).ConfigureAwait(false);
            var contentType = ContentTypes.GetValueOrDefault(
                Path.GetExtension(key), response.Headers.ContentType ?? "application/octet-stream");
            return (response.ResponseStream, contentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        if (KeyPattern().IsMatch(key))
        {
            await _client.DeleteObjectAsync(_bucket, key, ct).ConfigureAwait(false);
        }
    }

    public void Dispose() => _client.Dispose();

    [GeneratedRegex("^[a-z0-9-]{1,32}$")]
    private static partial Regex CategoryPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}/[a-z0-9-]{1,32}/[0-9a-f]{32}\.[a-z]{2,5}$")]
    private static partial Regex KeyPattern();
}
