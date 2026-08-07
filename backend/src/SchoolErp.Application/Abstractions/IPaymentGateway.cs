namespace SchoolErp.Application.Abstractions;

/// <summary>A verified, parsed gateway webhook event.</summary>
public sealed record GatewayEvent(string GatewayOrderId, string GatewayPaymentId, bool Succeeded);

/// <summary>
/// Online payment gateway (Razorpay/PayU/Cashfree in production, a
/// deterministic dev implementation locally). Amounts are INR.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates a gateway order and returns its id.</summary>
    Task<string> CreateOrderAsync(decimal amount, string receiptHint, CancellationToken ct = default);

    /// <summary>
    /// Verifies the webhook signature and parses the event. Returns null when
    /// the signature is invalid — the caller must then reject the request.
    /// </summary>
    GatewayEvent? VerifyWebhook(string body, string signature);
}
