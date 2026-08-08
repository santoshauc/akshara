using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Users;
using SchoolErp.Domain.Auth;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Infrastructure.Identity;

/// <summary>
/// Identity-backed staff/role administration, always scoped to the current
/// tenant. Role membership is managed through role IDs resolved within the
/// tenant — role NAMES repeat across schools, so the name-based Identity
/// helpers must not be used here.
/// </summary>
public sealed class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public UserAdminService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _userManager = userManager;
        _db = db;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _clock = clock;
    }

    private Guid TenantId => _tenantContext.TenantId;

    public async Task<IReadOnlyList<StaffUserDto>> GetUsersAsync(
        string? search, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking()
            .Where(u => u.TenantId == TenantId &&
                        _db.UserRoles.Any(ur => ur.UserId == u.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                EF.Functions.ILike(u.FullName, $"%{term}%") ||
                (u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%")) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Select(u => new StaffUserDto(
                u.Id,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                _db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                    .OrderBy(n => n)
                    .ToList(),
                u.IsActive))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<Guid> CreateUserAsync(
        string fullName, string? email, string? phone, string temporaryPassword,
        IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        if (email is not null && await _db.Users.AnyAsync(
                u => u.TenantId == TenantId && u.NormalizedEmail == email.ToUpperInvariant(), ct)
            .ConfigureAwait(false))
        {
            throw new ConflictException($"A user with email '{email}' already exists.");
        }

        if (phone is not null && await _db.Users.AnyAsync(
                u => u.TenantId == TenantId && u.PhoneNumber == phone, ct)
            .ConfigureAwait(false))
        {
            throw new ConflictException($"A user with phone '{phone}' already exists.");
        }

        var roleIds = await ResolveRoleIdsAsync(roles, ct).ConfigureAwait(false);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            FullName = fullName.Trim(),
            Email = email,
            EmailConfirmed = email is not null,
            PhoneNumber = phone,
            PhoneNumberConfirmed = phone is not null,
            TenantId = TenantId,
        };
        var created = await _userManager.CreateAsync(user, temporaryPassword).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            throw new ConflictException(string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        foreach (var roleId in roleIds)
        {
            _db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = roleId });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return user.Id;
    }

    public async Task UpdateUserAsync(
        Guid userId, string fullName, bool isActive, IReadOnlyList<string> roles,
        CancellationToken ct = default)
    {
        var user = await GetTenantUserAsync(userId, ct).ConfigureAwait(false);

        if (!isActive && string.Equals(
                _currentUser.UserId, userId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("You cannot deactivate your own account.");
        }

        var roleIds = await ResolveRoleIdsAsync(roles, ct).ConfigureAwait(false);

        user.FullName = fullName.Trim();
        var deactivated = user.IsActive && !isActive;
        user.IsActive = isActive;

        var existing = await _db.UserRoles.Where(ur => ur.UserId == user.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        _db.UserRoles.RemoveRange(existing);
        foreach (var roleId in roleIds)
        {
            _db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = roleId });
        }

        if (deactivated)
        {
            await RevokeSessionsAsync(user.Id, "deactivated", ct).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await GetTenantUserAsync(userId, ct).ConfigureAwait(false);

        // Remove+Add works without the token-provider machinery.
        var removed = await _userManager.RemovePasswordAsync(user).ConfigureAwait(false);
        if (!removed.Succeeded)
        {
            throw new ConflictException(string.Join(" ", removed.Errors.Select(e => e.Description)));
        }

        var added = await _userManager.AddPasswordAsync(user, newPassword).ConfigureAwait(false);
        if (!added.Succeeded)
        {
            throw new ConflictException(string.Join(" ", added.Errors.Select(e => e.Description)));
        }

        await RevokeSessionsAsync(user.Id, "password-reset", ct).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default) =>
        await _db.Roles.AsNoTracking()
            .Where(r => r.TenantId == TenantId)
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id,
                r.Name!,
                r.Description,
                _db.RoleClaims
                    .Where(c => c.RoleId == r.Id && c.ClaimType == Permissions.ClaimType)
                    .Select(c => c.ClaimValue!)
                    .OrderBy(p => p)
                    .ToList(),
                r.Name == WellKnownRoles.SchoolAdmin))
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<Guid> CreateRoleAsync(
        string name, string? description, IReadOnlyList<string> permissions,
        CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        if (await _db.Roles.AnyAsync(
                r => r.TenantId == TenantId && r.NormalizedName == trimmed.ToUpperInvariant(), ct)
            .ConfigureAwait(false))
        {
            throw new ConflictException($"Role '{trimmed}' already exists.");
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            NormalizedName = trimmed.ToUpperInvariant(),
            TenantId = TenantId,
            Description = description?.Trim(),
        };
        _db.Roles.Add(role);
        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
        {
            _db.RoleClaims.Add(new IdentityRoleClaim<Guid>
            {
                RoleId = role.Id,
                ClaimType = Permissions.ClaimType,
                ClaimValue = permission,
            });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return role.Id;
    }

    public async Task UpdateRoleAsync(
        Guid roleId, string? description, IReadOnlyList<string> permissions,
        CancellationToken ct = default)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == TenantId, ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Role", roleId);

        if (role.Name == WellKnownRoles.SchoolAdmin)
        {
            // The startup claims backfill would resurrect removed permissions
            // anyway — SchoolAdmin is by definition "everything".
            throw new ConflictException("The SchoolAdmin role is managed by the system.");
        }

        role.Description = description?.Trim();

        var existing = await _db.RoleClaims
            .Where(c => c.RoleId == role.Id && c.ClaimType == Permissions.ClaimType)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        _db.RoleClaims.RemoveRange(existing);
        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
        {
            _db.RoleClaims.Add(new IdentityRoleClaim<Guid>
            {
                RoleId = role.Id,
                ClaimType = Permissions.ClaimType,
                ClaimValue = permission,
            });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<ApplicationUser> GetTenantUserAsync(Guid userId, CancellationToken ct) =>
        await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == TenantId, ct)
            .ConfigureAwait(false)
        ?? throw new NotFoundException("User", userId);

    private async Task<List<Guid>> ResolveRoleIdsAsync(
        IReadOnlyList<string> roles, CancellationToken ct)
    {
        var wanted = roles.Select(r => r.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var found = await _db.Roles.AsNoTracking()
            .Where(r => r.TenantId == TenantId && wanted.Contains(r.Name!))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var missing = wanted.Except(found.Select(f => f.Name!), StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException("Role", missing[0]);
        }

        return found.Select(f => f.Id).ToList();
    }

    private async Task RevokeSessionsAsync(Guid userId, string reason, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var active = await _db.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevocationReason = reason;
        }
    }
}
