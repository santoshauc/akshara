using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.IntegrationTests.Tenancy;

/// <summary>
/// Proves the two independent tenant-isolation layers against a real
/// PostgreSQL 16 instance: EF Core global query filters, and row-level
/// security policies (verified with the EF filters deliberately bypassed).
/// </summary>
public sealed class TenantIsolationTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private Guid _schoolA;
    private Guid _schoolB;

    public TenantIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    /// <summary>Seeds two schools and one academic year for School A.</summary>
    public async Task InitializeAsync()
    {
        _schoolA = Guid.NewGuid();
        _schoolB = Guid.NewGuid();

        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.Tenants.AddRange(
                NewTenant(_schoolA, "SCHA"),
                NewTenant(_schoolB, "SCHB"));
            await admin.SaveChangesAsync();
        }

        await using (var contextA = _fixture.CreateAppContext(_schoolA))
        {
            contextA.AcademicYears.Add(new AcademicYear
            {
                Name = "2026-27",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2027, 4, 30),
                IsCurrent = true,
            });
            await contextA.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync()
    {
        // Each test seeds fresh tenants, so wipe business rows between tests.
        await using var admin = _fixture.CreateAdminContext();
        await admin.Database.ExecuteSqlRawAsync(
            "DELETE FROM academic_years; DELETE FROM tenants;");
    }

    [Fact]
    public async Task QueryFilters_HideOtherTenantsRows()
    {
        await using var contextB = _fixture.CreateAppContext(_schoolB);
        (await contextB.AcademicYears.ToListAsync()).Should().BeEmpty(
            "School B must never see School A's data");

        await using var contextA = _fixture.CreateAppContext(_schoolA);
        (await contextA.AcademicYears.ToListAsync()).Should().ContainSingle(
            "School A must still see its own data");
    }

    [Fact]
    public async Task Rls_BlocksAccess_EvenWhenEfFiltersAreBypassed()
    {
        // IgnoreQueryFilters simulates a bug (or malicious query) that strips
        // the first isolation layer. RLS must still return zero foreign rows.
        await using var contextB = _fixture.CreateAppContext(_schoolB);
        var leaked = await contextB.AcademicYears.IgnoreQueryFilters().ToListAsync();
        leaked.Should().BeEmpty("row-level security is the defense when EF filters are bypassed");

        await using var contextA = _fixture.CreateAppContext(_schoolA);
        var own = await contextA.AcademicYears.IgnoreQueryFilters().ToListAsync();
        own.Should().ContainSingle("RLS must not hide a tenant's own rows");
    }

    [Fact]
    public async Task Rls_ReturnsNothing_WhenNoTenantIsBound()
    {
        // A connection with no tenant session variable (e.g. a misconfigured
        // background job) must see zero business rows — even via raw SQL.
        await using var unbound = _fixture.CreateAppContext(tenantId: null);

        var count = await unbound.Database
            .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM academic_years")
            .SingleAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Insert_IsStampedWithAmbientTenant_IgnoringClientValue()
    {
        await using (var contextB = _fixture.CreateAppContext(_schoolB))
        {
            contextB.AcademicYears.Add(new AcademicYear
            {
                // Malicious/buggy attempt to write into School A.
                TenantId = _schoolA,
                Name = "2026-27",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2027, 4, 30),
            });
            await contextB.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateAdminContext();
        var row = await verify.AcademicYears
            .IgnoreQueryFilters()
            .SingleAsync(y => y.TenantId == _schoolB);
        row.Name.Should().Be("2026-27");
    }

    [Fact]
    public async Task TenantId_CannotBeChangedAfterCreation()
    {
        await using var contextA = _fixture.CreateAppContext(_schoolA);
        var year = await contextA.AcademicYears.SingleAsync();

        year.TenantId = _schoolB;
        var act = () => contextA.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must never change*");
    }

    [Fact]
    public async Task Delete_IsConvertedToSoftDelete_AndHiddenFromQueries()
    {
        await using (var contextA = _fixture.CreateAppContext(_schoolA))
        {
            var year = await contextA.AcademicYears.SingleAsync();
            contextA.AcademicYears.Remove(year);
            await contextA.SaveChangesAsync();
        }

        await using var contextA2 = _fixture.CreateAppContext(_schoolA);
        (await contextA2.AcademicYears.ToListAsync()).Should().BeEmpty(
            "soft-deleted rows are hidden from normal queries");

        // The row still physically exists for audit/statutory retention.
        await using var admin = _fixture.CreateAdminContext();
        var raw = await admin.AcademicYears.IgnoreQueryFilters()
            .SingleAsync(y => y.TenantId == _schoolA);
        raw.IsDeleted.Should().BeTrue();
    }

    private static Tenant NewTenant(Guid id, string code) => new()
    {
        Id = id,
        Code = code,
        Name = $"School {code}",
        Subdomain = code.ToLowerInvariant(),
        Status = TenantStatus.Active,
    };
}
