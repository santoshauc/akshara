using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.TenantCatalog.Commands;

/// <summary>Activates, suspends or archives a school.</summary>
public sealed record ChangeTenantStatusCommand(Guid Id, TenantStatus Status) : IRequest;

/// <summary>Guards the tenant lifecycle state machine.</summary>
public sealed class ChangeTenantStatusCommandValidator : AbstractValidator<ChangeTenantStatusCommand>
{
    public ChangeTenantStatusCommandValidator()
    {
        RuleFor(c => c.Status).IsInEnum();
    }
}

/// <summary>Applies the status change with transition validation.</summary>
public sealed class ChangeTenantStatusCommandHandler : IRequestHandler<ChangeTenantStatusCommand>
{
    private readonly IApplicationDbContext _db;

    public ChangeTenantStatusCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ChangeTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Tenant), request.Id);

        // Archived is terminal: an archived school is kept for statutory
        // retention only and must be re-onboarded to return.
        if (tenant.Status == TenantStatus.Archived && request.Status != TenantStatus.Archived)
        {
            throw new ConflictException("An archived school cannot be reactivated.");
        }

        tenant.Status = request.Status;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
