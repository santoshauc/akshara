using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Admissions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.Admissions;

/// <summary>
/// An enquiry submitted from a school's public website — anonymous, so it
/// carries the school code and resolves the tenant itself (the same pattern
/// as OTP requests). Lands in the school's admissions CRM as a New enquiry
/// with source Website.
/// </summary>
public sealed record SubmitPublicEnquiryCommand(
    string SchoolCode,
    string ChildName,
    DateOnly? DateOfBirth,
    string AppliedClass,
    string ParentName,
    string Phone,
    string? Email,
    string? Notes) : IRequest;

/// <summary>Same shape rules as staff-entered enquiries.</summary>
public sealed class SubmitPublicEnquiryCommandValidator
    : AbstractValidator<SubmitPublicEnquiryCommand>
{
    public SubmitPublicEnquiryCommandValidator()
    {
        RuleFor(c => c.SchoolCode).NotEmpty().MaximumLength(8);
        RuleFor(c => c.ChildName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("The child's name is required.")
            .MaximumLength(200);
        RuleFor(c => c.AppliedClass)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("The class applied for is required.")
            .MaximumLength(100);
        RuleFor(c => c.ParentName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("The parent's name is required.")
            .MaximumLength(200);
        RuleFor(c => c.Phone).NotEmpty().Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("The phone number must be 10–15 digits, optionally starting with +.");
        RuleFor(c => c.Email).EmailAddress().MaximumLength(320)
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

/// <summary>Resolves the school, pins a scope to it, files the enquiry.</summary>
public sealed class SubmitPublicEnquiryCommandHandler : IRequestHandler<SubmitPublicEnquiryCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;

    public SubmitPublicEnquiryCommandHandler(
        IApplicationDbContext db, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _scopeFactory = scopeFactory;
    }

    public async Task Handle(SubmitPublicEnquiryCommand request, CancellationToken cancellationToken)
    {
        var code = request.SchoolCode.Trim().ToUpperInvariant();
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Code == code && t.Status == TenantStatus.Active)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.SchoolCode);

        // The same phone knocking twice just refreshes interest — don't
        // duplicate an open pipeline row (public forms get resubmitted a lot).
        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenant.Id);
        var tenantDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var phone = request.Phone.Trim();
        var alreadyOpen = await tenantDb.AdmissionEnquiries.AnyAsync(e =>
                e.Phone == phone &&
                e.Status != EnquiryStatus.Admitted && e.Status != EnquiryStatus.Lost,
            cancellationToken).ConfigureAwait(false);
        if (alreadyOpen)
        {
            return; // silently accepted — the enquiry is already being worked
        }

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new CreateEnquiryCommand(
                request.ChildName,
                request.DateOfBirth,
                request.AppliedClass,
                request.ParentName,
                phone,
                request.Email,
                EnquirySource.Website,
                FollowUpOn: null,
                request.Notes),
            cancellationToken).ConfigureAwait(false);
    }
}
