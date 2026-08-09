using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure.Identity;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Infrastructure.Persistence.Seeding;

/// <summary>
/// Development-only seed data: a platform Super Admin, one demo school with a
/// School Admin and a parent. Never wired in production — the production
/// onboarding path is the tenant module + user provisioning APIs.
/// </summary>
public static partial class DevSeeder
{
    public const string SuperAdminEmail = "superadmin@schoolerp.local";
    public const string DemoSchoolCode = "DEMO01";
    public const string DemoAdminEmail = "admin@demo.school";
    public const string DemoParentPhone = "+919000000001";

    /// <summary>Default password for every seeded dev account.</summary>
    public const string Password = "ChangeMe@12345";

    /// <summary>Seeds idempotently; skips silently when migrations are pending.</summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DevSeeder");

        var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
        if (pending.Any())
        {
            LogPendingMigrations(logger);
            return;
        }

        // Always runs: seeded SchoolAdmin roles hold every permission, so any
        // constants added since the original seed are backfilled here. (Users
        // still need to sign out/in — permission claims live in the JWT.)
        await BackfillSchoolAdminClaimsAsync(db, logger).ConfigureAwait(false);
        await BackfillDemoEntitlementsAsync(db, logger).ConfigureAwait(false);

        if (await db.Users.AnyAsync().ConfigureAwait(false))
        {
            // Shell already exists — just keep the demo dataset topped up.
            await DemoDataSeeder.SeedAsync(services).ConfigureAwait(false);
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // --- Platform Super Admin -----------------------------------------
        var superRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = WellKnownRoles.SuperAdmin,
            NormalizedName = WellKnownRoles.SuperAdmin.ToUpperInvariant(),
            TenantId = null,
            Description = "Platform operator; implicitly holds every permission.",
        };
        db.Roles.Add(superRole);

        var superAdmin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Email = SuperAdminEmail,
            FullName = "Platform Super Admin",
            TenantId = null,
            EmailConfirmed = true,
        };
        await CreateUserAsync(userManager, superAdmin).ConfigureAwait(false);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = superAdmin.Id, RoleId = superRole.Id });

        // --- Demo school ---------------------------------------------------
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = DemoSchoolCode,
            Name = "Demo Public School",
            Subdomain = "demo",
            City = "Hyderabad",
            State = "Telangana",
            AffiliationBoard = "CBSE",
            Plan = SubscriptionPlan.Standard,
            EnabledModules = DemoModules,
            SmsCredits = 10_000,
            Status = TenantStatus.Active,
        };
        db.Tenants.Add(tenant);

        var adminRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = WellKnownRoles.SchoolAdmin,
            NormalizedName = WellKnownRoles.SchoolAdmin.ToUpperInvariant(),
            TenantId = tenant.Id,
            Description = "Full administrative access within the school.",
        };
        db.Roles.Add(adminRole);
        db.RoleClaims.AddRange(Permissions.All.Select(p => new IdentityRoleClaim<Guid>
        {
            RoleId = adminRole.Id,
            ClaimType = Permissions.ClaimType,
            ClaimValue = p,
        }));

        var schoolAdmin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Email = DemoAdminEmail,
            PhoneNumber = "+919000000000",
            FullName = "Demo School Admin",
            TenantId = tenant.Id,
            EmailConfirmed = true,
        };
        await CreateUserAsync(userManager, schoolAdmin).ConfigureAwait(false);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = schoolAdmin.Id, RoleId = adminRole.Id });

        var parent = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Email = "parent@demo.school",
            PhoneNumber = DemoParentPhone,
            FullName = "Demo Parent",
            TenantId = tenant.Id,
            PhoneNumberConfirmed = true,
        };
        await CreateUserAsync(userManager, parent).ConfigureAwait(false);

        await db.SaveChangesAsync().ConfigureAwait(false);
        LogSeeded(logger, SuperAdminEmail, DemoAdminEmail, DemoSchoolCode);

        // Freshly created shell — enrich it in the same startup so a brand-new
        // environment is demo-ready immediately.
        await DemoDataSeeder.SeedAsync(services).ConfigureAwait(false);
    }

    /// <summary>Adds any missing permission claims to seeded SchoolAdmin roles.</summary>
    private static async Task BackfillSchoolAdminClaimsAsync(AppDbContext db, ILogger logger)
    {
        var adminRoleIds = await db.Roles
            .Where(r => r.Name == WellKnownRoles.SchoolAdmin)
            .Select(r => r.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var added = 0;
        foreach (var roleId in adminRoleIds)
        {
            var existing = await db.RoleClaims
                .Where(c => c.RoleId == roleId && c.ClaimType == Permissions.ClaimType)
                .Select(c => c.ClaimValue!)
                .ToListAsync()
                .ConfigureAwait(false);

            var missing = Permissions.All.Except(existing, StringComparer.Ordinal).ToList();
            db.RoleClaims.AddRange(missing.Select(p => new IdentityRoleClaim<Guid>
            {
                RoleId = roleId,
                ClaimType = Permissions.ClaimType,
                ClaimValue = p,
            }));
            added += missing.Count;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
            LogClaimsBackfilled(logger, added);
        }
    }

    /// <summary>Every module SchoolErp has actually shipped, for the demo school.</summary>
    private const TenantModules DemoModules =
        TenantModules.Core | TenantModules.Examination | TenantModules.Fees |
        TenantModules.Transport | TenantModules.Library | TenantModules.Timetable |
        TenantModules.Homework | TenantModules.Hostel;

    /// <summary>
    /// Keeps the demo school's entitlements current as modules ship: enables
    /// every built module and tops SMS credits back up when they run out, so
    /// local demos never trip the plan-enforcement gates unintentionally.
    /// </summary>
    private static async Task BackfillDemoEntitlementsAsync(AppDbContext db, ILogger logger)
    {
        var demo = await db.Tenants
            .FirstOrDefaultAsync(t => t.Code == DemoSchoolCode)
            .ConfigureAwait(false);
        if (demo is null)
        {
            return;
        }

        var changed = false;
        if ((demo.EnabledModules & DemoModules) != DemoModules)
        {
            demo.EnabledModules |= DemoModules;
            changed = true;
        }

        if (demo.SmsCredits <= 0)
        {
            demo.SmsCredits = 10_000;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
            LogDemoEntitlements(logger);
        }
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        var result = await userManager.CreateAsync(user, Password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Dev seed failed for {user.Email}: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "DevSeeder skipped: pending migrations. Run 'dotnet ef database update' first.")]
    private static partial void LogPendingMigrations(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Dev data seeded. Platform: {SuperAdmin} | School admin: {SchoolAdmin} (code {SchoolCode})")]
    private static partial void LogSeeded(ILogger logger, string superAdmin, string schoolAdmin, string schoolCode);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Backfilled {Count} missing permission claims onto SchoolAdmin roles; users must re-login to receive them")]
    private static partial void LogClaimsBackfilled(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Demo school entitlements refreshed: all shipped modules enabled, SMS credits topped up")]
    private static partial void LogDemoEntitlements(ILogger logger);
}
