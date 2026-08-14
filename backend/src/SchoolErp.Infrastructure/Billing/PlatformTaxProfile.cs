using Microsoft.Extensions.Configuration;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Billing;

/// <summary>
/// Reads the operator's GST registration from the <c>Billing</c> section:
/// <c>Gstin</c>, <c>GstState</c>, <c>SacCode</c>, <c>GstRatePercent</c>.
/// Setting <c>Billing:Gstin</c> is the switch that turns invoices into tax
/// invoices — same activation pattern as Razorpay and MSG91.
/// <para>
/// Values are read LIVE per access rather than captured in the constructor, so
/// registering for GST is a configuration change and a restartless reload, not
/// a deploy. (Frozen-at-issue semantics still hold — the invoice handler copies
/// these onto the row; this class only answers "what is true right now".)
/// </para>
/// <para>
/// Defaults: SAC 997331 (licensing of software) and 18% are the values most
/// commonly correct for SaaS, but both stay configurable because they are the
/// operator's CA's call, not this codebase's.
/// </para>
/// </summary>
public sealed class PlatformTaxProfile : IPlatformTaxProfile
{
    private readonly IConfiguration _configuration;

    public PlatformTaxProfile(IConfiguration configuration) => _configuration = configuration;

    public bool IsRegistered => Gstin.Length > 0;

    public string Gstin => _configuration["Billing:Gstin"]?.Trim() ?? string.Empty;

    public string? State => _configuration["Billing:GstState"]?.Trim();

    public string SacCode =>
        _configuration["Billing:SacCode"]?.Trim() is { Length: > 0 } sac ? sac : "997331";

    public decimal RatePercent =>
        decimal.TryParse(_configuration["Billing:GstRatePercent"], out var rate) ? rate : 18m;
}
