using FluentAssertions;
using SchoolErp.Domain.Auth;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Domain.Timetable;

namespace SchoolErp.UnitTests.Domain;

/// <summary>
/// The handful of real decisions the domain entities make for themselves.
///
/// Almost everything in Domain is state that something else interprets, but
/// these four properties answer questions the rest of the system trusts without
/// re-checking: is this session still valid, may this school serve traffic, is
/// this slot a taught period, what do we call this guardian. Two of them are
/// security-relevant and combine more than one field, which is exactly the shape
/// that breaks quietly when a field is added.
/// </summary>
public sealed class EntityRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_refresh_token_inside_its_window_is_active()
    {
        var token = new RefreshToken { ExpiresAt = Now.AddDays(1) };

        token.IsActive(Now).Should().BeTrue();
    }

    [Fact]
    public void An_expired_refresh_token_is_not_active()
    {
        var token = new RefreshToken { ExpiresAt = Now.AddSeconds(-1) };

        token.IsActive(Now).Should().BeFalse();
    }

    [Fact]
    public void A_revoked_refresh_token_is_dead_even_before_it_expires()
    {
        // Revocation is how "sign out this device" and reuse-detection work. If
        // an unexpired-but-revoked token still read as active, revoking a stolen
        // session would do nothing until it aged out on its own.
        var token = new RefreshToken { ExpiresAt = Now.AddDays(7), RevokedAt = Now.AddMinutes(-5) };

        token.IsActive(Now).Should().BeFalse();
    }

    [Fact]
    public void A_token_expiring_exactly_now_is_not_active()
    {
        // The comparison is strict, so the boundary belongs to expiry. Pinned
        // because flipping it to <= would silently extend every session.
        var token = new RefreshToken { ExpiresAt = Now };

        token.IsActive(Now).Should().BeFalse();
    }

    [Fact]
    public void An_active_school_may_serve_traffic()
    {
        var tenant = new Tenant { Status = TenantStatus.Active };

        tenant.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(TenantStatus.Provisioning)]
    [InlineData(TenantStatus.Suspended)]
    public void A_school_that_is_not_active_may_not_serve_traffic(TenantStatus status)
    {
        // Suspension is the lever behind unpaid-invoice enforcement, and
        // Provisioning is the state before onboarding finishes.
        var tenant = new Tenant { Status = status };

        tenant.IsActive.Should().BeFalse();
    }

    [Fact]
    public void A_soft_deleted_school_is_inactive_even_while_its_status_says_active()
    {
        // The property is an AND of two independent fields, and this is the half
        // that is easy to forget: deletion is soft, so the Status column is left
        // exactly as it was. Reading Status alone would let a deleted school
        // keep serving.
        var tenant = new Tenant { Status = TenantStatus.Active, IsDeleted = true };

        tenant.IsActive.Should().BeFalse();
    }

    [Fact]
    public void A_taught_period_is_not_a_break()
    {
        var entry = new TimetableEntry { SlotKind = TimetableSlotKind.Lesson };

        entry.IsBreak.Should().BeFalse();
    }

    [Theory]
    [InlineData(TimetableSlotKind.Break)]
    [InlineData(TimetableSlotKind.Lunch)]
    public void Recess_and_lunch_are_breaks(TimetableSlotKind kind)
    {
        // Breaks must not be offered as attendance roll-call slots or counted as
        // periods-per-week in teaching-load insights.
        var entry = new TimetableEntry { SlotKind = kind };

        entry.IsBreak.Should().BeTrue();
    }

    [Fact]
    public void A_guardian_reads_as_first_name_then_last_name()
    {
        var guardian = new Guardian { FirstName = "Priya", LastName = "Reddy" };

        guardian.FullName.Should().Be("Priya Reddy");
    }

    [Fact]
    public void A_guardian_with_only_one_name_does_not_carry_a_trailing_space()
    {
        // Mononyms are ordinary in India, and this name is printed on receipts,
        // certificates and SMS. A trailing space would show up in all of them.
        var guardian = new Guardian { FirstName = "Selvi", LastName = string.Empty };

        guardian.FullName.Should().Be("Selvi");
    }
}
