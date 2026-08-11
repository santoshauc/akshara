using FluentAssertions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.UnitTests.TenantCatalog;

/// <summary>
/// What each plan sells. These are commercial rules living in code: the module
/// bundle decides what a school can open, and the rate decides what the annual
/// licence invoice says. Both are read by things a customer notices — the module
/// gate answers 403 from the first, and BillingCycleJob invoices from the second
/// — so a careless edit here is a billing or entitlement incident, not a bug.
/// </summary>
public sealed class PlanPresetTests
{
    [Fact]
    public void Every_paid_plan_includes_the_core_module()
    {
        // Core carries SIS, attendance and communication. A plan without it
        // would sell a school an ERP it cannot log into meaningfully.
        foreach (var plan in Enum.GetValues<SubscriptionPlan>())
        {
            PlanPresets.ModulesFor(plan).Should().HaveFlag(TenantModules.Core,
                $"{plan} must include Core");
        }
    }

    [Fact]
    public void The_plans_are_strictly_nested_so_upgrading_never_removes_a_module()
    {
        // A school upgrading Basic -> Standard -> Premium must never LOSE a
        // module in the process. Nesting is what guarantees that, and it is easy
        // to break by editing one preset in isolation.
        var basic = PlanPresets.ModulesFor(SubscriptionPlan.Basic);
        var standard = PlanPresets.ModulesFor(SubscriptionPlan.Standard);
        var premium = PlanPresets.ModulesFor(SubscriptionPlan.Premium);
        var enterprise = PlanPresets.ModulesFor(SubscriptionPlan.Enterprise);

        standard.Should().HaveFlag(basic);
        premium.Should().HaveFlag(standard);
        enterprise.Should().HaveFlag(premium);
    }

    [Theory]
    [InlineData(SubscriptionPlan.Basic, TenantModules.Examination)]
    [InlineData(SubscriptionPlan.Basic, TenantModules.Fees)]
    [InlineData(SubscriptionPlan.Standard, TenantModules.Transport)]
    [InlineData(SubscriptionPlan.Standard, TenantModules.Library)]
    [InlineData(SubscriptionPlan.Standard, TenantModules.Timetable)]
    [InlineData(SubscriptionPlan.Standard, TenantModules.Homework)]
    [InlineData(SubscriptionPlan.Premium, TenantModules.Hostel)]
    [InlineData(SubscriptionPlan.Premium, TenantModules.FrontOffice)]
    [InlineData(SubscriptionPlan.Enterprise, TenantModules.Inventory)]
    public void A_plan_includes_what_it_is_sold_as_including(
        SubscriptionPlan plan, TenantModules expected) =>
        PlanPresets.ModulesFor(plan).Should().HaveFlag(expected);

    [Theory]
    [InlineData(SubscriptionPlan.Basic, TenantModules.Transport)]
    [InlineData(SubscriptionPlan.Basic, TenantModules.Library)]
    [InlineData(SubscriptionPlan.Standard, TenantModules.Hostel)]
    [InlineData(SubscriptionPlan.Standard, TenantModules.FrontOffice)]
    [InlineData(SubscriptionPlan.Premium, TenantModules.Inventory)]
    public void A_plan_does_not_quietly_include_what_the_tier_above_sells(
        SubscriptionPlan plan, TenantModules notExpected) =>
        PlanPresets.ModulesFor(plan).Should().NotHaveFlag(notExpected);

    [Fact]
    public void No_plan_bundles_human_resources_because_nothing_implements_it()
    {
        // The source comment is explicit that payroll (PF, ESI, TDS, gratuity)
        // is its own product and the flag is unimplemented — "selling it would
        // be selling an empty module". This test is what stops someone adding
        // it to Everything for tidiness and shipping an empty promise.
        foreach (var plan in Enum.GetValues<SubscriptionPlan>())
        {
            PlanPresets.ModulesFor(plan).Should().NotHaveFlag(TenantModules.HumanResources,
                $"{plan} must not sell an unimplemented module");
        }
    }

    [Fact]
    public void A_trial_sees_everything_because_the_expiry_date_is_the_limiter()
    {
        // Trials are limited by SubscriptionExpiresOn (login returns 423 once it
        // passes), not by withholding modules — otherwise a trial cannot
        // demonstrate what is being bought.
        PlanPresets.ModulesFor(SubscriptionPlan.Trial)
            .Should().Be(PlanPresets.ModulesFor(SubscriptionPlan.Enterprise));
    }

    [Theory]
    [InlineData(SubscriptionPlan.Basic, 45)]
    [InlineData(SubscriptionPlan.Standard, 70)]
    [InlineData(SubscriptionPlan.Premium, 120)]
    [InlineData(SubscriptionPlan.Enterprise, 150)]
    public void The_licence_rate_matches_the_published_price(
        SubscriptionPlan plan, decimal expected) =>
        PlanPresets.AnnualRatePerStudent(plan).Should().Be(expected);

    [Fact]
    public void A_trial_is_not_invoiced()
    {
        // BillingCycleJob multiplies this by the student count. Anything other
        // than zero would auto-invoice schools that have not bought anything.
        PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Trial).Should().Be(0m);
    }

    [Fact]
    public void The_rate_rises_with_the_tier()
    {
        // Ordering is the invariant worth pinning; the exact numbers above will
        // change with pricing, but a cheaper premium tier is always a mistake.
        PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Basic)
            .Should().BeLessThan(PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Standard));
        PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Standard)
            .Should().BeLessThan(PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Premium));
        PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Premium)
            .Should().BeLessThan(PlanPresets.AnnualRatePerStudent(SubscriptionPlan.Enterprise));
    }
}
