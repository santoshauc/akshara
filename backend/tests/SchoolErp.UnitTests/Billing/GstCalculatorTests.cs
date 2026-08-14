using FluentAssertions;
using SchoolErp.Domain.Billing;

namespace SchoolErp.UnitTests.Billing;

/// <summary>
/// The GST arithmetic on platform invoices. Two decisions live here and both
/// end up on a legal document: whether a supply is intra-state (CGST+SGST) or
/// inter-state (IGST), and how the components round. Getting either wrong is a
/// tax filing problem for the operator, not a cosmetic one.
/// </summary>
public sealed class GstCalculatorTests
{
    // Telangana (36) supplier; Telangana and Karnataka (29) buyers.
    private const string SupplierGstin = "36AAAAA0000A1Z5";
    private const string SameStateBuyer = "36BBBBB1111B1Z6";
    private const string OtherStateBuyer = "29CCCCC2222C1Z7";

    [Fact]
    public void A_buyer_registered_in_the_same_state_is_intra_state() =>
        GstCalculator.IsIntraState(SupplierGstin, "Telangana", SameStateBuyer, null)
            .Should().BeTrue();

    [Fact]
    public void A_buyer_registered_in_another_state_is_inter_state() =>
        GstCalculator.IsIntraState(SupplierGstin, "Telangana", OtherStateBuyer, null)
            .Should().BeFalse();

    [Fact]
    public void The_gstin_outranks_a_contradictory_address()
    {
        // The GSTIN's two-digit prefix IS the state of registration, validated
        // at issue; the address is free text a clerk typed. When they disagree,
        // the registration wins.
        GstCalculator.IsIntraState(SupplierGstin, "Telangana", OtherStateBuyer, "Telangana")
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("Telangana", true)]
    [InlineData("telangana", true)]  // free-text field; case must not decide tax
    [InlineData(" Telangana ", true)]
    [InlineData("Karnataka", false)]
    public void An_unregistered_buyer_falls_back_to_the_state_name(string buyerState, bool expected) =>
        GstCalculator.IsIntraState(SupplierGstin, "Telangana", null, buyerState)
            .Should().Be(expected);

    [Fact]
    public void A_buyer_with_no_state_at_all_defaults_to_intra_state()
    {
        // For an unidentifiable recipient the place of supply defaults to the
        // supplier's own location. Also the conservative error: the right TOTAL
        // reaches the wrong ledgers rather than the wrong total reaching anyone.
        GstCalculator.IsIntraState(SupplierGstin, "Telangana", null, null)
            .Should().BeTrue();
    }

    [Fact]
    public void Intra_state_splits_the_rate_into_equal_halves()
    {
        var split = GstCalculator.Split(10_000m, 18m, intraState: true);

        split.Cgst.Should().Be(900m);
        split.Sgst.Should().Be(900m);
        split.Igst.Should().Be(0m);
        split.Total.Should().Be(1_800m);
    }

    [Fact]
    public void Inter_state_levies_the_whole_rate_as_igst()
    {
        var split = GstCalculator.Split(10_000m, 18m, intraState: false);

        split.Cgst.Should().Be(0m);
        split.Sgst.Should().Be(0m);
        split.Igst.Should().Be(1_800m);
    }

    [Fact]
    public void Components_round_to_the_paise_independently_so_the_printed_lines_sum_exactly()
    {
        // 9% of 1,111 is 99.99 even; 9% of 1,111.11 is 99.9999 -> 100.00 each.
        // Whatever the inputs, the invoice must show components that ADD UP to
        // the tax actually charged - Total is defined as their sum, so asserting
        // the components pins the whole contract.
        var split = GstCalculator.Split(1_111.11m, 18m, intraState: true);

        split.Cgst.Should().Be(100.00m);
        split.Sgst.Should().Be(100.00m);
        split.Total.Should().Be(200.00m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void Nothing_taxable_means_no_tax(decimal taxable) =>
        GstCalculator.Split(taxable, 18m, intraState: true).Total.Should().Be(0m);

    [Fact]
    public void A_zero_rate_means_no_tax() =>
        GstCalculator.Split(10_000m, 0m, intraState: true).Total.Should().Be(0m);
}
