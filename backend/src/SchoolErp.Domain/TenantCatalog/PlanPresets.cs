namespace SchoolErp.Domain.TenantCatalog;

/// <summary>
/// What each subscription plan includes out of the box. Presets are a
/// starting point — the operator can still hand-tune modules per school;
/// enforcement always reads the school's actual module flags.
/// </summary>
public static class PlanPresets
{
    private const TenantModules Basic =
        TenantModules.Core | TenantModules.Examination | TenantModules.Fees;

    private const TenantModules Standard = Basic |
        TenantModules.Transport | TenantModules.Library |
        TenantModules.Timetable | TenantModules.Homework;

    private const TenantModules Premium = Standard |
        TenantModules.Hostel | TenantModules.FrontOffice;

    // HumanResources is deliberately NOT bundled: payroll (PF, ESI, TDS,
    // gratuity) is its own product and nothing implements the flag yet.
    // Selling it would be selling an empty module.
    private const TenantModules Everything = Premium | TenantModules.Inventory;

    /// <summary>The module bundle a plan ships with.</summary>
    public static TenantModules ModulesFor(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Basic => Basic,
        SubscriptionPlan.Standard => Standard,
        SubscriptionPlan.Premium => Premium,
        SubscriptionPlan.Enterprise => Everything,
        // Trials see everything — the expiry date is the limiter.
        _ => Everything,
    };

    /// <summary>Suggested per-student annual licence rate (₹), for invoicing.</summary>
    public static decimal AnnualRatePerStudent(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Basic => 45m,
        SubscriptionPlan.Standard => 70m,
        SubscriptionPlan.Premium => 120m,
        SubscriptionPlan.Enterprise => 150m,
        _ => 0m,
    };
}
