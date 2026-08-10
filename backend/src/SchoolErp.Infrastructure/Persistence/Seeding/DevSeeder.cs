using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Campuses;
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

    /// <summary>
    /// A second demo tenant that is a COLLEGE, not a school. Without one there
    /// is no way to see the departments/programmes side of the product.
    /// </summary>
    public const string DemoCollegeCode = "COLL01";
    public const string DemoCollegeAdminEmail = "admin@demo.college";

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

        // Runs at every startup rather than only on a fresh database, so an
        // environment seeded before colleges existed still gets one.
        await EnsureDemoCollegeAsync(scope.ServiceProvider, db, logger).ConfigureAwait(false);

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
            Affiliations = [new TenantAffiliation { Board = "CBSE", AffiliationNumber = "1234567" }],
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
        db.RoleClaims.AddRange(Permissions.TenantAssignable.Select(p => new IdentityRoleClaim<Guid>
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

    /// <summary>
    /// Adds any missing tenant-assignable permission claims to seeded
    /// SchoolAdmin roles — and STRIPS platform-only claims that earlier seeds
    /// granted (a school role must never reach the school catalog or
    /// platform billing).
    /// </summary>
    private static async Task BackfillSchoolAdminClaimsAsync(AppDbContext db, ILogger logger)
    {
        var adminRoleIds = await db.Roles
            .Where(r => r.Name == WellKnownRoles.SchoolAdmin && r.TenantId != null)
            .Select(r => r.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var added = 0;
        var removed = 0;
        foreach (var roleId in adminRoleIds)
        {
            var existing = await db.RoleClaims
                .Where(c => c.RoleId == roleId && c.ClaimType == Permissions.ClaimType)
                .ToListAsync()
                .ConfigureAwait(false);

            var missing = Permissions.TenantAssignable
                .Except(existing.Select(c => c.ClaimValue!), StringComparer.Ordinal)
                .ToList();
            db.RoleClaims.AddRange(missing.Select(p => new IdentityRoleClaim<Guid>
            {
                RoleId = roleId,
                ClaimType = Permissions.ClaimType,
                ClaimValue = p,
            }));
            added += missing.Count;

            var platformClaims = existing
                .Where(c => Permissions.PlatformOnly.Contains(c.ClaimValue!, StringComparer.Ordinal))
                .ToList();
            db.RoleClaims.RemoveRange(platformClaims);
            removed += platformClaims.Count;
        }

        if (added > 0 || removed > 0)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
            LogClaimsBackfilled(logger, added, removed);
        }
    }

    /// <summary>
    /// A demo college: tenant, admin, two departments, three programmes and
    /// the cohorts they teach. Deliberately has no students — the platform
    /// dashboard then flags it as "active but nobody enrolled", which is both
    /// true and a live demonstration of that check.
    /// </summary>
    private static async Task EnsureDemoCollegeAsync(
        IServiceProvider services, AppDbContext db, ILogger logger)
    {
        var existing = await db.Tenants
            .FirstOrDefaultAsync(t => t.Code == DemoCollegeCode)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            // Already here, but possibly seeded by an earlier version of this
            // method — top up whatever that one did not create.
            await TopUpCollegeAcademicsAsync(services, existing.Id).ConfigureAwait(false);
            return;
        }

        var college = new Tenant
        {
            Id = Guid.NewGuid(),
            Code = DemoCollegeCode,
            Name = "Demo Degree College",
            Subdomain = "democollege",
            InstitutionType = InstitutionType.College,
            AddressLine1 = "Survey No. 21, Gachibowli",
            City = "Hyderabad",
            State = "Telangana",
            PostalCode = "500032",
            ContactPhone = "+914066778899",
            Plan = SubscriptionPlan.Premium,
            EnabledModules = DemoModules,
            SmsCredits = 5_000,
            Status = TenantStatus.Active,
        };
        db.Tenants.Add(college);

        var adminRole = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = WellKnownRoles.SchoolAdmin,
            NormalizedName = WellKnownRoles.SchoolAdmin.ToUpperInvariant(),
            TenantId = college.Id,
            Description = "Full administrative access within the college.",
        };
        db.Roles.Add(adminRole);
        db.RoleClaims.AddRange(Permissions.TenantAssignable.Select(p => new IdentityRoleClaim<Guid>
        {
            RoleId = adminRole.Id,
            ClaimType = Permissions.ClaimType,
            ClaimValue = p,
        }));

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = Guid.NewGuid().ToString("N"),
            Email = DemoCollegeAdminEmail,
            PhoneNumber = "+919000000010",
            FullName = "Demo College Admin",
            TenantId = college.Id,
            EmailConfirmed = true,
        };
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        await CreateUserAsync(userManager, admin).ConfigureAwait(false);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync().ConfigureAwait(false);

        // Everything below lives in RLS'd tables, so it has to be written from
        // a scope BOUND to the new college: the policy is FORCEd, and with
        // app.tenant_id unset its WITH CHECK would reject every row.
        await using var collegeScope = services
            .GetRequiredService<IServiceScopeFactory>()
            .CreateAsyncScope();
        collegeScope.ServiceProvider.GetRequiredService<Application.Abstractions.ITenantContextSetter>()
            .SetTenant(college.Id);
        var collegeDb = collegeScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The primary campus every institution has, same as the migration
        // backfill gives the schools that predate campuses.
        collegeDb.Campuses.Add(new Campus
        {
            Name = "Main Campus",
            Code = "MAIN",
            AddressLine1 = college.AddressLine1,
            City = college.City,
            State = college.State,
            PostalCode = college.PostalCode,
            ContactPhone = college.ContactPhone,
            IsPrimary = true,
            IsActive = true,
        });

        // Without a current year nobody can be admitted, which would make the
        // whole college unusable rather than merely empty.
        collegeDb.AcademicYears.Add(new AcademicYear
        {
            Name = "2026-27",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2027, 4, 30),
            IsCurrent = true,
        });

        var computing = new Department { Name = "Computer Science", Code = "CSE" };
        var commerce = new Department { Name = "Commerce", Code = "COM" };
        collegeDb.Departments.AddRange(computing, commerce);

        var btech = new Programme
        {
            DepartmentId = computing.Id,
            Name = "B.Tech Computer Science", Code = "BTCSE",
            Level = ProgrammeLevel.Undergraduate, DurationYears = 4, TermsPerYear = 2,
        };
        var mca = new Programme
        {
            DepartmentId = computing.Id,
            Name = "Master of Computer Applications", Code = "MCA",
            Level = ProgrammeLevel.Postgraduate, DurationYears = 2, TermsPerYear = 2,
        };
        var bcom = new Programme
        {
            DepartmentId = commerce.Id,
            Name = "B.Com General", Code = "BCOM",
            Level = ProgrammeLevel.Undergraduate, DurationYears = 3, TermsPerYear = 2,
        };
        collegeDb.Programmes.AddRange(btech, mca, bcom);

        // Cohorts are ordinary classes pointed at a programme — that reuse is
        // what makes attendance, timetables and exams work for a college.
        collegeDb.SchoolClasses.Add(new SchoolClass
        {
            Name = "B.Tech CSE Semester 1", DisplayOrder = 1, ProgrammeId = btech.Id,
            Sections = [new Section { Name = "A" }, new Section { Name = "B" }],
        });
        collegeDb.SchoolClasses.Add(new SchoolClass
        {
            Name = "B.Com Semester 1", DisplayOrder = 2, ProgrammeId = bcom.Id,
            Sections = [new Section { Name = "A" }],
        });

        await collegeDb.SaveChangesAsync().ConfigureAwait(false);
        LogCollegeSeeded(logger, DemoCollegeCode, DemoCollegeAdminEmail);
    }

    /// <summary>
    /// Adds the pieces of the demo college that a previously-seeded database
    /// may be missing. Only the academic year so far — without a current year
    /// nobody can be admitted, which makes the college unusable rather than
    /// merely empty.
    /// </summary>
    private static async Task TopUpCollegeAcademicsAsync(IServiceProvider services, Guid collegeId)
    {
        await using var scope = services
            .GetRequiredService<IServiceScopeFactory>()
            .CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<Application.Abstractions.ITenantContextSetter>()
            .SetTenant(collegeId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.AcademicYears.AnyAsync().ConfigureAwait(false))
        {
            return;
        }

        db.AcademicYears.Add(new AcademicYear
        {
            Name = "2026-27",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2027, 4, 30),
            IsCurrent = true,
        });
        await db.SaveChangesAsync().ConfigureAwait(false);
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
        Message = "SchoolAdmin claims reconciled: {Added} added, {Removed} platform-only removed; users must re-login to receive them")]
    private static partial void LogClaimsBackfilled(ILogger logger, int added, int removed);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Demo school entitlements refreshed: all shipped modules enabled, SMS credits topped up")]
    private static partial void LogDemoEntitlements(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Demo college seeded: {Code} | admin {Admin} (departments, programmes and cohorts included)")]
    private static partial void LogCollegeSeeded(ILogger logger, string code, string admin);
}
