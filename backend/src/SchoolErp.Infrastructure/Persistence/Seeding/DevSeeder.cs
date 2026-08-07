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

        if (await db.Users.AnyAsync().ConfigureAwait(false))
        {
            return; // already seeded
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
            EnabledModules = TenantModules.Core | TenantModules.Examination |
                             TenantModules.Fees | TenantModules.Transport,
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
}
