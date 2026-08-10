using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.TenantCatalog;

/// <summary>
/// What login screens and apps need before anyone signs in: the school's
/// name, logo and colours. Intentionally nothing else — this is served
/// anonymously by school code.
/// </summary>
public sealed record TenantBrandingDto(
    string Name,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    // School or college. Here rather than in the token because the portal
    // already fetches branding to theme itself, and a claim would only reach
    // the UI after the next sign-in. Not sensitive: an institution's own
    // website says which it is.
    InstitutionType InstitutionType = InstitutionType.School);

/// <summary>Branding for one school code (anonymous; 404 on unknown codes).</summary>
public sealed record GetTenantBrandingQuery(string Code) : IRequest<TenantBrandingDto>;

/// <summary>Looks the school up by code, case-insensitively.</summary>
public sealed class GetTenantBrandingQueryHandler
    : IRequestHandler<GetTenantBrandingQuery, TenantBrandingDto>
{
    private readonly IApplicationDbContext _db;

    public GetTenantBrandingQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TenantBrandingDto> Handle(
        GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        return await _db.Tenants.AsNoTracking()
            .Where(t => t.Code == code)
            .Select(t => new TenantBrandingDto(
                t.Name, t.LogoUrl, t.ThemePrimaryColor, t.ThemeSecondaryColor,
                t.InstitutionType))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.Code);
    }
}

/// <summary>
/// Uploads/replaces a school's logo and returns its public URL. The old
/// logo file is deleted so storage doesn't accumulate orphans.
/// </summary>
public sealed record UploadTenantLogoCommand(
    Guid TenantId, string Extension, byte[] Content) : IRequest<string>;

/// <summary>Stores the file under the target school and stamps LogoUrl.</summary>
public sealed class UploadTenantLogoCommandHandler
    : IRequestHandler<UploadTenantLogoCommand, string>
{
    private const string FileRoutePrefix = "/api/v1/files/";

    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public UploadTenantLogoCommandHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<string> Handle(
        UploadTenantLogoCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

        var oldKey = tenant.LogoUrl?.StartsWith(FileRoutePrefix, StringComparison.Ordinal) == true
            ? tenant.LogoUrl[FileRoutePrefix.Length..]
            : null;

        using var stream = new MemoryStream(request.Content);
        var key = await _storage.SaveAsync(
                tenant.Id, "branding", request.Extension, stream, cancellationToken)
            .ConfigureAwait(false);
        tenant.LogoUrl = $"{FileRoutePrefix}{key}";
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (oldKey is not null)
        {
            await _storage.DeleteAsync(oldKey, cancellationToken).ConfigureAwait(false);
        }

        return tenant.LogoUrl;
    }
}
