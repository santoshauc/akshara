using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Platform;
using SchoolErp.Domain.Auth;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.Infrastructure.Identity;

/// <summary>
/// Platform operator accounts — the people who can administer every school.
/// Separate from the tenant-scoped user service on purpose: everything here
/// works on <c>TenantId == null</c> rows, and sharing code with the school
/// path is how one would eventually appear in a school's user list.
/// </summary>
public sealed class PlatformOperatorService : IPlatformOperatorService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public PlatformOperatorService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        ICurrentUser currentUser,
        TimeProvider clock)
    {
        _userManager = userManager;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PlatformOperatorDto>> GetOperatorsAsync(
        CancellationToken ct = default) =>
        await _db.Users
            .Where(u => u.TenantId == null)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new PlatformOperatorDto(
                u.Id, u.FullName, u.Email, u.PhoneNumber,
                u.IsActive, u.TwoFactorEnabled, u.CreatedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<Guid> CreateOperatorAsync(
        string fullName, string email, string temporaryPassword, CancellationToken ct = default)
    {
        var trimmed = email.Trim();
        var normalized = trimmed.ToUpperInvariant();
        if (await _db.Users
                .AnyAsync(u => u.TenantId == null && u.NormalizedEmail == normalized, ct)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"An operator with email '{trimmed}' already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            FullName = fullName.Trim(),
            Email = trimmed,
            EmailConfirmed = true,
            TenantId = null,
            CreatedAt = _clock.GetUtcNow(),
        };

        var created = await _userManager.CreateAsync(user, temporaryPassword).ConfigureAwait(false);
        if (!created.Succeeded)
        {
            throw new ConflictException(
                string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.TenantId == null && r.Name == WellKnownRoles.SuperAdmin, ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Role", WellKnownRoles.SuperAdmin);

        _db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return user.Id;
    }

    public async Task SetOperatorActiveAsync(
        Guid operatorId, bool isActive, CancellationToken ct = default)
    {
        var user = await FindOperatorAsync(operatorId, ct).ConfigureAwait(false);

        if (!isActive)
        {
            // Two ways to lock everyone out of the platform, both refused.
            if (string.Equals(
                    _currentUser.UserId, operatorId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("You cannot disable your own operator account.");
            }

            var othersActive = await _db.Users
                .CountAsync(u => u.TenantId == null && u.IsActive && u.Id != operatorId, ct)
                .ConfigureAwait(false);
            if (othersActive == 0)
            {
                throw new ConflictException(
                    "This is the last active operator; the platform would be left unadministrable.");
            }
        }

        user.IsActive = isActive;
        if (!isActive)
        {
            await RevokeSessionsAsync(user.Id, ct).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task ResetOperatorPasswordAsync(
        Guid operatorId, string newPassword, CancellationToken ct = default)
    {
        var user = await FindOperatorAsync(operatorId, ct).ConfigureAwait(false);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        var reset = await _userManager.ResetPasswordAsync(user, token, newPassword)
            .ConfigureAwait(false);
        if (!reset.Succeeded)
        {
            throw new ConflictException(
                string.Join(" ", reset.Errors.Select(e => e.Description)));
        }

        await RevokeSessionsAsync(user.Id, ct).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<ApplicationUser> FindOperatorAsync(Guid operatorId, CancellationToken ct) =>
        await _db.Users
            .FirstOrDefaultAsync(u => u.Id == operatorId && u.TenantId == null, ct)
            .ConfigureAwait(false)
        ?? throw new NotFoundException("Operator", operatorId);

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var active = await _db.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var token in active)
        {
            token.RevokedAt = now;
        }
    }
}
