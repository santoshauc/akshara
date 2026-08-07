using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SchoolErp.Infrastructure.Payments;

namespace SchoolErp.UnitTests.Payments;

/// <summary>Razorpay adapter behavior without touching the real API.</summary>
public sealed class RazorpayGatewayTests
{
    private const string WebhookSecret = "whsec_test_123";

    private static RazorpayGateway CreateGateway(FakeHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.razorpay.com/") },
            Options.Create(new RazorpayOptions
            {
                KeyId = "rzp_test_key",
                KeySecret = "rzp_test_secret",
                WebhookSecret = WebhookSecret,
            }),
            NullLogger<RazorpayGateway>.Instance);

    [Fact]
    public async Task CreateOrder_SendsBasicAuthAndPaise_AndReturnsOrderId()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"id":"order_Nxy123","status":"created"}""");
        var gateway = CreateGateway(handler);

        var orderId = await gateway.CreateOrderAsync(1234.50m, "RCP-2026-0042");

        orderId.Should().Be("order_Nxy123");
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/v1/orders");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(
                handler.LastRequest.Headers.Authorization.Parameter!))
            .Should().Be("rzp_test_key:rzp_test_secret");

        using var sent = JsonDocument.Parse(handler.LastBody!);
        sent.RootElement.GetProperty("amount").GetInt64()
            .Should().Be(123450, "Razorpay amounts are integer paise");
        sent.RootElement.GetProperty("currency").GetString().Should().Be("INR");
        sent.RootElement.GetProperty("receipt").GetString().Should().Be("RCP-2026-0042");
    }

    [Fact]
    public async Task CreateOrder_OnGatewayError_ThrowsWithStatus()
    {
        var handler = new FakeHandler(
            HttpStatusCode.Unauthorized, """{"error":{"description":"Authentication failed"}}""");
        var gateway = CreateGateway(handler);

        var act = () => gateway.CreateOrderAsync(100m, "RCP-1");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
    }

    [Fact]
    public void VerifyWebhook_AcceptsGenuineCapturedEvent_AndRejectsTampering()
    {
        var gateway = CreateGateway(new FakeHandler(HttpStatusCode.OK, "{}"));
        const string body = """
            {"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_ABC","order_id":"order_XYZ","status":"captured"}}}}
            """;
        var signature = Sign(body);

        var parsed = gateway.VerifyWebhook(body, signature);
        parsed.Should().NotBeNull();
        parsed!.GatewayOrderId.Should().Be("order_XYZ");
        parsed.GatewayPaymentId.Should().Be("pay_ABC");
        parsed.Succeeded.Should().BeTrue();

        gateway.VerifyWebhook(body + " ", signature)
            .Should().BeNull("any change to the body must invalidate the signature");
        gateway.VerifyWebhook(body, Sign("something else"))
            .Should().BeNull();
    }

    [Fact]
    public void VerifyWebhook_FailedPayment_ParsesAsUnsuccessful()
    {
        var gateway = CreateGateway(new FakeHandler(HttpStatusCode.OK, "{}"));
        const string body = """
            {"event":"payment.failed","payload":{"payment":{"entity":{"id":"pay_DEF","order_id":"order_XYZ","status":"failed"}}}}
            """;

        var parsed = gateway.VerifyWebhook(body, Sign(body));
        parsed.Should().NotBeNull();
        parsed!.Succeeded.Should().BeFalse();
    }

    private static string Sign(string body) =>
        Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(WebhookSecret), Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();

    /// <summary>Captures the outgoing request and replies with a canned response.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;

        public FakeHandler(HttpStatusCode status, string responseBody)
        {
            _status = status;
            _responseBody = responseBody;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
