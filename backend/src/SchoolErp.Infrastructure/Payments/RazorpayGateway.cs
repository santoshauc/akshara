using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Payments;

/// <summary>Razorpay credentials. The gateway is active only when KeyId is set.</summary>
public sealed class RazorpayOptions
{
    public const string SectionName = "Razorpay";

    public string KeyId { get; set; } = string.Empty;

    public string KeySecret { get; set; } = string.Empty;

    /// <summary>Secret configured on the Razorpay webhook, not the API key pair.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>
/// Razorpay adapter over the Orders API (basic auth, amounts in paise) and
/// the standard X-Razorpay-Signature webhook scheme (HMAC-SHA256 hex of the
/// raw body with the webhook secret). No SDK — two endpoints via HttpClient.
/// </summary>
public sealed partial class RazorpayGateway : IPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly RazorpayOptions _options;
    private readonly ILogger<RazorpayGateway> _logger;

    public RazorpayGateway(
        HttpClient http, IOptions<RazorpayOptions> options, ILogger<RazorpayGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.KeyId}:{_options.KeySecret}")));
    }

    public async Task<string> CreateOrderAsync(
        decimal amount, string receiptHint, CancellationToken ct = default)
    {
        // Razorpay amounts are integer paise; receipts are limited to 40 chars.
        var payload = new
        {
            amount = (long)decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero),
            currency = "INR",
            receipt = receiptHint.Length <= 40 ? receiptHint : receiptHint[..40],
        };

        var response = await _http.PostAsJsonAsync("v1/orders", payload, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            LogOrderFailed(_logger, (int)response.StatusCode);
            throw new InvalidOperationException(
                $"Razorpay order creation failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        var order = JsonSerializer.Deserialize<OrderResponse>(body, JsonOptions);
        if (string.IsNullOrWhiteSpace(order?.Id))
        {
            throw new InvalidOperationException("Razorpay order response had no id.");
        }

        return order.Id;
    }

    public GatewayEvent? VerifyWebhook(string body, string signature)
    {
        var expected = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_options.WebhookSecret),
                Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant())))
        {
            return null;
        }

        try
        {
            var webhook = JsonSerializer.Deserialize<WebhookBody>(body, JsonOptions);
            var payment = webhook?.Payload?.Payment?.Entity;
            if (webhook?.Event is null || payment?.OrderId is null || payment.Id is null)
            {
                return null;
            }

            return new GatewayEvent(
                payment.OrderId,
                payment.Id,
                string.Equals(webhook.Event, "payment.captured", StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 512 ? value : value[..512];

    private sealed record OrderResponse(string? Id);

    private sealed record WebhookBody(
        [property: JsonPropertyName("event")] string? Event,
        [property: JsonPropertyName("payload")] WebhookPayload? Payload);

    private sealed record WebhookPayload(
        [property: JsonPropertyName("payment")] WebhookPayment? Payment);

    private sealed record WebhookPayment(
        [property: JsonPropertyName("entity")] PaymentEntity? Entity);

    private sealed record PaymentEntity(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("order_id")] string? OrderId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Razorpay order creation failed with HTTP {StatusCode}")]
    private static partial void LogOrderFailed(ILogger logger, int statusCode);
}
