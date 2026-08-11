using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Fees;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// The hosted checkout page — anonymous by necessity, because a parent follows
/// a link to it from an SMS and is not signed in to anything.
///
/// The one that matters here is dev-complete. It marks an order PAID through the
/// real webhook path, it takes no credentials, and the only thing standing
/// between it and the internet is an IsDevelopment() check. If that check is
/// ever weakened, anybody who can guess an order id can settle a fee bill
/// without paying, and no other test in this suite would notice.
/// </summary>
[Collection(ApiCollectionDefinition.Name)]
public sealed class CheckoutSurfaceTests
{
    private readonly ApiFixture _api;

    public CheckoutSurfaceTests(ApiFixture api) => _api = api;

    /// <summary>Puts a payment order in the database and returns its gateway id.</summary>
    private async Task<string> SeedOrderAsync(PaymentOrderStatus status)
    {
        var gatewayOrderId = $"order_{Guid.NewGuid():N}";

        await using var scope = _api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(_api.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.PaymentOrders.Add(new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = _api.TenantId,
            StudentId = Guid.NewGuid(),
            AcademicYearId = _api.AcademicYearId,
            Amount = 25_000m,
            GatewayOrderId = gatewayOrderId,
            Status = status,
        });
        await db.SaveChangesAsync();

        return gatewayOrderId;
    }

    [Fact]
    public async Task The_simulate_endpoint_does_not_exist_outside_development()
    {
        // THE ONE THAT MATTERS. Anonymous, and it settles a bill. Outside
        // Development it must be indistinguishable from a route that was never
        // mapped - a 404, not a 403, so it does not even confirm the feature
        // exists.
        var gatewayOrderId = await SeedOrderAsync(PaymentOrderStatus.Created);
        using var client = _api.CreateAnonymousClient();

        var response = await client.PostAsync(
            new Uri($"/api/v1/payments/checkout/{gatewayOrderId}/dev-complete", UriKind.Relative),
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_order_that_was_not_settled_stays_unsettled()
    {
        // The status assertion above proves the HTTP answer; this proves the
        // side effect did not happen anyway. A 404 returned after the work was
        // already done would be the worst of both.
        var gatewayOrderId = await SeedOrderAsync(PaymentOrderStatus.Created);
        using var client = _api.CreateAnonymousClient();

        await client.PostAsync(
            new Uri($"/api/v1/payments/checkout/{gatewayOrderId}/dev-complete", UriKind.Relative),
            content: null);

        await using var scope = _api.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(_api.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.PaymentOrders.AsNoTracking()
            .FirstAsync(o => o.GatewayOrderId == gatewayOrderId);

        order.Status.Should().Be(PaymentOrderStatus.Created);
        order.GatewayPaymentId.Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_order_has_no_checkout_page()
    {
        // Order ids arrive in URLs from SMS links, so a wrong or stale one is
        // ordinary traffic rather than an attack.
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/payments/checkout/order_{Guid.NewGuid():N}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_open_order_renders_a_checkout_page_to_a_signed_out_parent()
    {
        // The parent following the link has no session. If this ever started
        // requiring one, fee collection by SMS link would silently stop working.
        var gatewayOrderId = await SeedOrderAsync(PaymentOrderStatus.Created);
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/payments/checkout/{gatewayOrderId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        (await response.Content.ReadAsStringAsync()).Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public async Task An_already_paid_order_says_so_instead_of_offering_to_charge_again()
    {
        // Parents re-open SMS links. Showing a live payment form for a settled
        // bill invites a double payment and a refund conversation.
        var gatewayOrderId = await SeedOrderAsync(PaymentOrderStatus.Paid);
        using var client = _api.CreateAnonymousClient();

        var response = await client.GetAsync(
            new Uri($"/api/v1/payments/checkout/{gatewayOrderId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("already");
        html.Should().Contain("completed");
    }
}
