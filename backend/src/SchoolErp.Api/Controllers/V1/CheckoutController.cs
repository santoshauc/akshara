using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Domain.Fees;
using SchoolErp.Infrastructure.Payments;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// The hosted checkout page for fee payments — opened in a browser by the
/// parent app on any platform. Keyed by the unguessable gateway order id.
/// With Razorpay configured it loads the official checkout; with the dev
/// gateway it offers a simulate button that drives the real webhook path.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/payments/checkout")]
public sealed class CheckoutController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly GatewayWebhookProcessor _webhookProcessor;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public CheckoutController(
        IDbContextFactory<AppDbContext> contextFactory,
        GatewayWebhookProcessor webhookProcessor,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _contextFactory = contextFactory;
        _webhookProcessor = webhookProcessor;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>The checkout page for one order.</summary>
    [HttpGet("{gatewayOrderId}")]
    [AllowAnonymous]
    [Produces("text/html")]
    public async Task<IActionResult> GetCheckoutPage(string gatewayOrderId, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var order = await db.PaymentOrders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.GatewayOrderId == gatewayOrderId, ct);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status != PaymentOrderStatus.Created)
        {
            return Content(Page($"""
                <h2>✅ This payment is already {(order.Status == PaymentOrderStatus.Paid ? "completed" : "closed")}.</h2>
                <p>You can close this page.</p>
                """), "text/html");
        }

        var razorpayKey = _configuration["Razorpay:KeyId"];
        if (!string.IsNullOrWhiteSpace(razorpayKey))
        {
            var paise = (long)decimal.Round(order.Amount * 100, 0, MidpointRounding.AwayFromZero);
            return Content(Page($$"""
                <h2>School fee payment</h2>
                <p>Amount: ₹{{order.Amount:0.##}}</p>
                <p id="status">Opening secure checkout…</p>
                <script src="https://checkout.razorpay.com/v1/checkout.js"></script>
                <script>
                  var rzp = new Razorpay({
                    key: '{{razorpayKey}}',
                    order_id: '{{gatewayOrderId}}',
                    amount: {{paise}},
                    currency: 'INR',
                    name: 'School fee payment',
                    handler: function () {
                      document.getElementById('status').textContent =
                        'Payment submitted. The receipt arrives by SMS shortly — you can close this page.';
                    }
                  });
                  rzp.open();
                </script>
                """), "text/html");
        }

        // Dev gateway: a visible simulate button that exercises the REAL
        // webhook path (signed body → processor → receipt → SMS).
        return Content(Page($$"""
            <h2>School fee payment (development)</h2>
            <p>Amount: ₹{{order.Amount:0.##}}</p>
            <button id="pay" onclick="pay()">Simulate successful payment</button>
            <p id="status"></p>
            <script>
              async function pay() {
                document.getElementById('pay').disabled = true;
                const response = await fetch(
                  '/api/v1/payments/checkout/{{gatewayOrderId}}/dev-complete', { method: 'POST' });
                document.getElementById('status').textContent = response.ok
                  ? '✅ Payment completed. The receipt arrives by SMS — you can close this page.'
                  : '❌ Simulation failed (' + response.status + ').';
              }
            </script>
            """), "text/html");
    }

    /// <summary>Development-only: completes a dev-gateway order via the real webhook path.</summary>
    [HttpPost("{gatewayOrderId}/dev-complete")]
    [AllowAnonymous]
    public async Task<IActionResult> DevComplete(string gatewayOrderId, CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var body = JsonSerializer.Serialize(new DevPaymentGateway.DevWebhookBody(
            gatewayOrderId, $"dev_pay_{Guid.NewGuid():N}", "paid"));
        var secret = _configuration["Payments:WebhookSecret"] ?? "dev-webhook-secret";
        var signature = DevPaymentGateway.Sign(body, secret);

        return await _webhookProcessor.ProcessAsync(body, signature, ct)
            ? NoContent()
            : BadRequest();
    }

    private static string Page(string body) => $$"""
        <!DOCTYPE html>
        <html><head><meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>Fee payment — SchoolErp</title>
        <style>
          body { font-family: system-ui, sans-serif; max-width: 480px; margin: 48px auto;
                 padding: 0 16px; color: #223; }
          button { background: #1565C0; color: #fff; border: 0; border-radius: 8px;
                   padding: 12px 20px; font-size: 16px; cursor: pointer; }
          button:disabled { opacity: .5; }
        </style></head>
        <body>{{body}}</body></html>
        """;
}
