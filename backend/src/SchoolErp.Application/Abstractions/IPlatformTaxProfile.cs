namespace SchoolErp.Application.Abstractions;

/// <summary>
/// The platform operator's own GST registration, as configured. Implemented in
/// Infrastructure over configuration and activated the way every provider here
/// is: no <c>Billing:Gstin</c> means <see cref="IsRegistered"/> is false and
/// invoices are issued plain — which is the CORRECT behaviour for an operator
/// below the registration threshold, not a degraded mode.
/// </summary>
public interface IPlatformTaxProfile
{
    bool IsRegistered { get; }

    /// <summary>The operator's GSTIN. Empty when not registered.</summary>
    string Gstin { get; }

    /// <summary>The operator's state of registration, for the place-of-supply decision.</summary>
    string? State { get; }

    /// <summary>Services Accounting Code to print. Configuration, not law baked into code.</summary>
    string SacCode { get; }

    /// <summary>Whole GST rate to apply, e.g. 18.</summary>
    decimal RatePercent { get; }
}
