using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.TenantCatalog.Commands;

/// <summary>
/// Updates a school's profile, branding and entitlements. Code and subdomain
/// are immutable after onboarding — they are login/routing identifiers.
/// </summary>
public sealed record UpdateTenantCommand(
    Guid Id,
    string Name,
    string? CustomDomain,
    string? ContactEmail,
    string? ContactPhone,
    string? City,
    string? State,
    IReadOnlyList<TenantAffiliationDto>? Affiliations,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    SubscriptionPlan Plan,
    DateOnly? SubscriptionExpiresOn,
    TenantModules EnabledModules,
    int StorageLimitMb,
    int SmsCredits,
    bool WhatsAppEnabled,
    string TimeZoneId,
    string DefaultLanguage,
    // Nullable so an omitted field leaves the institution type alone. A
    // defaulted value would quietly demote a college to a school every time a
    // client posted a body that predates this field.
    InstitutionType? InstitutionType = null,
    // The school's GST registration, printed on platform invoices as the
    // recipient GSTIN. Optional trailing param so older clients are untouched.
    string? Gstin = null) : IRequest<TenantDto>;

/// <summary>Shape rules for tenant updates.</summary>
public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);

        RuleFor(c => c.CustomDomain)
            .Matches(@"^(?!-)[a-z0-9-]{1,63}(?<!-)(\.[a-z0-9-]{1,63})+$")
            .When(c => !string.IsNullOrWhiteSpace(c.CustomDomain));

        RuleFor(c => c.ContactEmail).EmailAddress().MaximumLength(320)
            .When(c => !string.IsNullOrWhiteSpace(c.ContactEmail));

        RuleFor(c => c.StorageLimitMb).GreaterThan(0);
        RuleFor(c => c.SmsCredits).GreaterThanOrEqualTo(0);

        RuleFor(c => c.DefaultLanguage).Must(l => l is "en" or "te");

        RuleFor(c => c.EnabledModules)
            .Must(m => m.HasFlag(TenantModules.Core))
            .WithMessage("The Core module cannot be disabled.");

        // 15 chars: 2-digit state code, 10-char PAN, entity digit, 'Z', checksum.
        // Shape-checked here so a typo surfaces at the form, not on an issued
        // invoice; the checksum itself is the GST portal's job, not ours.
        RuleFor(c => c.Gstin)
            .Matches("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]$")
            .WithMessage("That does not look like a GSTIN (15 characters, e.g. 36ABCDE1234F1Z5).")
            .When(c => !string.IsNullOrWhiteSpace(c.Gstin));
    }
}

/// <summary>Applies the update with a custom-domain uniqueness check.</summary>
public sealed class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateTenantCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .Include(t => t.Affiliations)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.Id);

        var customDomain = request.CustomDomain?.ToLowerInvariant();
        if (customDomain is not null && customDomain != tenant.CustomDomain)
        {
            var taken = await _db.Tenants
                .AnyAsync(t => t.Id != tenant.Id && t.CustomDomain == customDomain, cancellationToken)
                .ConfigureAwait(false);
            if (taken)
            {
                throw new ConflictException("A school with this custom domain already exists.");
            }
        }

        tenant.Name = request.Name.Trim();
        tenant.CustomDomain = customDomain;
        tenant.ContactEmail = request.ContactEmail?.Trim();
        tenant.ContactPhone = request.ContactPhone?.Trim();
        tenant.City = request.City?.Trim();
        tenant.State = request.State?.Trim();
        // Uppercased: GSTINs are case-insensitive on entry but canonical in
        // caps, and the state-code prefix comparison depends on a stable form.
        tenant.Gstin = string.IsNullOrWhiteSpace(request.Gstin)
            ? null
            : request.Gstin.Trim().ToUpperInvariant();
        tenant.InstitutionType = request.InstitutionType ?? tenant.InstitutionType;
        ApplyAffiliations(tenant, request.Affiliations);
        tenant.LogoUrl = request.LogoUrl;
        tenant.ThemePrimaryColor = request.ThemePrimaryColor;
        tenant.ThemeSecondaryColor = request.ThemeSecondaryColor;
        tenant.Plan = request.Plan;
        tenant.SubscriptionExpiresOn = request.SubscriptionExpiresOn;
        tenant.EnabledModules = request.EnabledModules;
        tenant.StorageLimitMb = request.StorageLimitMb;
        tenant.SmsCredits = request.SmsCredits;
        tenant.WhatsAppEnabled = request.WhatsAppEnabled;
        tenant.TimeZoneId = request.TimeZoneId;
        tenant.DefaultLanguage = request.DefaultLanguage;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tenant.ToDto();
    }

    /// <summary>
    /// Replaces the affiliation set. Rows are matched by board so an existing
    /// affiliation keeps its id (and anything that ever references it) when
    /// only its number changes; blank boards are dropped and duplicates
    /// collapse, since the unique index would refuse them anyway.
    /// </summary>
    internal static void ApplyAffiliations(
        Tenant tenant, IReadOnlyList<TenantAffiliationDto>? requested)
    {
        var wanted = (requested ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.Board))
            .GroupBy(a => a.Board.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        foreach (var gone in tenant.Affiliations
                     .Where(existing => !wanted.Any(w =>
                         string.Equals(w.Board.Trim(), existing.Board, StringComparison.OrdinalIgnoreCase)))
                     .ToList())
        {
            tenant.Affiliations.Remove(gone);
        }

        foreach (var affiliation in wanted)
        {
            var board = affiliation.Board.Trim();
            var number = string.IsNullOrWhiteSpace(affiliation.AffiliationNumber)
                ? null
                : affiliation.AffiliationNumber.Trim();

            var existing = tenant.Affiliations.FirstOrDefault(a =>
                string.Equals(a.Board, board, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                // No TenantId or Id set here — the navigation fixes the FK up,
                // and EF generates the key. See TenantAffiliation.Id.
                tenant.Affiliations.Add(new TenantAffiliation
                {
                    Board = board,
                    AffiliationNumber = number,
                });
            }
            else
            {
                existing.AffiliationNumber = number;
            }
        }
    }
}
