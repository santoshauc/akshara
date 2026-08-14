namespace SchoolErp.Domain.Billing;

/// <summary>The three GST components of one taxable amount. Unused legs are zero.</summary>
public readonly record struct GstSplit(decimal Cgst, decimal Sgst, decimal Igst)
{
    public decimal Total => Cgst + Sgst + Igst;
}

/// <summary>
/// GST arithmetic for platform invoices, kept pure so the rules are unit-testable
/// the way the grade calculators are.
///
/// The one decision that matters is intra-state versus inter-state: supply within
/// the supplier's own state splits the rate into equal CGST and SGST halves,
/// while supply across a state border levies the whole rate as IGST. Everything
/// here implements that decision and nothing else — rates, SAC codes and the
/// supplier's registration live in configuration, and whether to tax AT ALL is
/// the caller's question (an operator below the registration threshold issues
/// plain invoices, which is correct, not a gap).
/// </summary>
public static class GstCalculator
{
    /// <summary>
    /// Whether a supply counts as intra-state.
    ///
    /// The buyer's GSTIN is the strongest evidence — its first two digits ARE the
    /// state code, entered once and validated, so it wins over the free-text
    /// address. An unregistered buyer falls back to comparing state names. A
    /// buyer whose state is unknown entirely is treated as intra-state, because
    /// for an unidentifiable recipient the place of supply defaults to the
    /// supplier's own location — and CGST+SGST to one's own state is also the
    /// conservative error: the right TOTAL tax reaches the wrong ledgers, rather
    /// than the wrong total reaching anyone.
    /// </summary>
    public static bool IsIntraState(
        string supplierGstin, string? supplierState, string? buyerGstin, string? buyerState)
    {
        if (!string.IsNullOrWhiteSpace(buyerGstin) && buyerGstin.Length >= 2 &&
            supplierGstin.Length >= 2)
        {
            return string.Equals(
                supplierGstin[..2], buyerGstin.Trim()[..2], StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(buyerState) && !string.IsNullOrWhiteSpace(supplierState))
        {
            return string.Equals(
                supplierState.Trim(), buyerState.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    /// <summary>
    /// Splits the tax on <paramref name="taxableAmount"/>. Each component is
    /// rounded to the paise INDEPENDENTLY — that is how the components appear on
    /// the invoice and in GST returns, so the printed lines must sum to the
    /// charged tax exactly, even when half of 18% of the amount does not land on
    /// a whole paise.
    /// </summary>
    public static GstSplit Split(decimal taxableAmount, decimal ratePercent, bool intraState)
    {
        if (taxableAmount <= 0 || ratePercent <= 0)
        {
            return new GstSplit(0, 0, 0);
        }

        if (intraState)
        {
            var half = Math.Round(taxableAmount * (ratePercent / 2) / 100m, 2);
            return new GstSplit(half, half, 0);
        }

        return new GstSplit(0, 0, Math.Round(taxableAmount * ratePercent / 100m, 2));
    }
}
