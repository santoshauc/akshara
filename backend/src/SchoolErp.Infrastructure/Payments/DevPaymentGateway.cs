using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Payments;

/// <summary>
/// Deterministic local gateway: orders are ids we mint ourselves; webhooks are
/// HMAC-SHA256-signed JSON, mirroring how Razorpay/Cashfree webhooks are
/// verified so the production adapter is a drop-in swap.
/// </summary>
public sealed class DevPaymentGateway : IPaymentGateway
{
    /// <summary>Webhook body shape for the dev gateway.</summary>
    public sealed record DevWebhookBody(string OrderId, string PaymentId, string Event);

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly string _secret;

    public DevPaymentGateway(IConfiguration configuration)
    {
        _secret = configuration["Payments:WebhookSecret"] ?? "dev-webhook-secret";
    }

    public Task<string> CreateOrderAsync(
        decimal amount, string receiptHint, CancellationToken ct = default) =>
        Task.FromResult($"dev_order_{Guid.NewGuid():N}");

    public GatewayEvent? VerifyWebhook(string body, string signature)
    {
        var expected = Sign(body, _secret);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature)))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<DevWebhookBody>(body, JsonOptions);
            if (parsed is null)
            {
                return null;
            }

            return new GatewayEvent(
                parsed.OrderId,
                parsed.PaymentId,
                string.Equals(parsed.Event, "paid", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>HMAC-SHA256 hex signature; exposed so tests can sign webhooks.</summary>
    public static string Sign(string body, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
}
