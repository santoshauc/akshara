using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.TenantCatalog.Commands;

/// <summary>Onboards a new school into the platform catalog (Super Admin only).</summary>
public sealed record CreateTenantCommand(
    string Code,
    string Name,
    string Subdomain,
    string? CustomDomain,
    string? ContactEmail,
    string? ContactPhone,
    string? City,
    string? State,
    IReadOnlyList<TenantAffiliationDto>? Affiliations,
    SubscriptionPlan Plan,
    TenantModules EnabledModules,
    string TimeZoneId = "Asia/Kolkata",
    string DefaultLanguage = "en") : IRequest<TenantDto>;

/// <summary>Shape rules for school onboarding.</summary>
public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty()
            .Matches("^[A-Z0-9]{4,8}$")
            .WithMessage("School code must be 4–8 uppercase letters or digits.");

        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);

        RuleFor(c => c.Subdomain)
            .NotEmpty()
            .Matches("^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$")
            .WithMessage("Subdomain must be 3–63 lowercase letters, digits or hyphens.");

        RuleFor(c => c.CustomDomain)
            .Matches(@"^(?!-)[a-z0-9-]{1,63}(?<!-)(\.[a-z0-9-]{1,63})+$")
            .When(c => !string.IsNullOrWhiteSpace(c.CustomDomain))
            .WithMessage("Custom domain must be a valid host name.");

        RuleFor(c => c.ContactEmail).EmailAddress().MaximumLength(320)
            .When(c => !string.IsNullOrWhiteSpace(c.ContactEmail));

        RuleFor(c => c.ContactPhone).Matches(@"^\+?[0-9]{10,15}$")
            .When(c => !string.IsNullOrWhiteSpace(c.ContactPhone));

        RuleFor(c => c.DefaultLanguage).Must(l => l is "en" or "te")
            .WithMessage("Supported languages are 'en' and 'te'.");

        RuleFor(c => c.EnabledModules)
            .Must(m => m.HasFlag(TenantModules.Core))
            .WithMessage("The Core module cannot be disabled.");
    }
}

/// <summary>Creates the tenant after uniqueness checks on code/subdomain/domain.</summary>
public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, TenantDto>
{
    private readonly IApplicationDbContext _db;

    public CreateTenantCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.ToUpperInvariant();
        var subdomain = request.Subdomain.ToLowerInvariant();
        var customDomain = request.CustomDomain?.ToLowerInvariant();

        var clash = await _db.Tenants
            .Where(t => t.Code == code || t.Subdomain == subdomain ||
                        (customDomain != null && t.CustomDomain == customDomain))
            .Select(t => new { t.Code, t.Subdomain, t.CustomDomain })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (clash is not null)
        {
            var field = clash.Code == code ? "code"
                : clash.Subdomain == subdomain ? "subdomain"
                : "custom domain";
            throw new ConflictException($"A school with this {field} already exists.");
        }

        var tenant = new Tenant
        {
            Code = code,
            Name = request.Name.Trim(),
            Subdomain = subdomain,
            CustomDomain = customDomain,
            ContactEmail = request.ContactEmail?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            City = request.City?.Trim(),
            State = request.State?.Trim(),
            Plan = request.Plan,
            EnabledModules = request.EnabledModules,
            TimeZoneId = request.TimeZoneId,
            DefaultLanguage = request.DefaultLanguage,
            Status = TenantStatus.Provisioning,
        };

        UpdateTenantCommandHandler.ApplyAffiliations(tenant, request.Affiliations);

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return tenant.ToDto();
    }
}
