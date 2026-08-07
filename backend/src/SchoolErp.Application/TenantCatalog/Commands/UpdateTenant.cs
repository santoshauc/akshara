using AutoMapper;
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
    string? AffiliationBoard,
    string? LogoUrl,
    string? ThemePrimaryColor,
    string? ThemeSecondaryColor,
    SubscriptionPlan Plan,
    DateOnly? SubscriptionExpiresOn,
    TenantModules EnabledModules,
    int StorageLimitMb,
    int SmsCredits,
    string TimeZoneId,
    string DefaultLanguage) : IRequest<TenantDto>;

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
    }
}

/// <summary>Applies the update with a custom-domain uniqueness check.</summary>
public sealed class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;

    public UpdateTenantCommandHandler(IApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
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
        tenant.AffiliationBoard = request.AffiliationBoard?.Trim();
        tenant.LogoUrl = request.LogoUrl;
        tenant.ThemePrimaryColor = request.ThemePrimaryColor;
        tenant.ThemeSecondaryColor = request.ThemeSecondaryColor;
        tenant.Plan = request.Plan;
        tenant.SubscriptionExpiresOn = request.SubscriptionExpiresOn;
        tenant.EnabledModules = request.EnabledModules;
        tenant.StorageLimitMb = request.StorageLimitMb;
        tenant.SmsCredits = request.SmsCredits;
        tenant.TimeZoneId = request.TimeZoneId;
        tenant.DefaultLanguage = request.DefaultLanguage;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return _mapper.Map<TenantDto>(tenant);
    }
}
