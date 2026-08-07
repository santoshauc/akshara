using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Common;

namespace SchoolErp.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps audit columns, converts hard deletes into soft deletes, and assigns
/// <see cref="TenantEntity.TenantId"/> from the ambient tenant context so that
/// business code can never write a row into another tenant.
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public AuditableEntityInterceptor(
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditRules(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.GetUtcNow();
        var userId = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    StampTenant(entry);
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    GuardTenantUnchanged(entry);
                    break;

                case EntityState.Deleted:
                    // Platform rule: rows are never hard-deleted through the ORM.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }

    private void StampTenant(EntityEntry<AuditableEntity> entry)
    {
        if (entry.Entity is not TenantEntity tenantEntity)
        {
            return;
        }

        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException(
                $"Cannot insert {entry.Metadata.ClrType.Name}: no tenant is bound to the current scope.");
        }

        // Overwrite unconditionally — a client-supplied TenantId is never trusted.
        tenantEntity.TenantId = _tenantContext.TenantId;
    }

    private static void GuardTenantUnchanged(EntityEntry<AuditableEntity> entry)
    {
        if (entry.Entity is not TenantEntity)
        {
            return;
        }

        var tenantProp = entry.Property(nameof(TenantEntity.TenantId));
        if (tenantProp.IsModified)
        {
            throw new InvalidOperationException(
                $"TenantId of {entry.Metadata.ClrType.Name} must never change after creation.");
        }
    }
}
