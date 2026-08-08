using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SchoolErp.Infrastructure.Notifications;

namespace SchoolErp.UnitTests.Notifications;

/// <summary>MSG91 adapter behavior without touching the real API.</summary>
public sealed class Msg91SmsSenderTests
{
    private static Msg91SmsSender CreateSender(FakeHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.msg91.com/") },
            Options.Create(new SmsOptions
            {
                Provider = "msg91",
                Msg91AuthKey = "test-auth-key",
                Msg91SenderId = "SCHERP",
                Msg91DltTemplateId = "1207100000000000000",
            }),
            NullLogger<Msg91SmsSender>.Instance);

    [Fact]
    public async Task Send_carries_auth_dlt_route_and_normalized_phone()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"type":"success","message":"ok"}""");
        var sender = CreateSender(handler);

        await sender.SendAsync("+919876501234", "Your ward was absent today.");

        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/v2/sendsms");
        handler.LastRequest.Headers.GetValues("authkey").Should().ContainSingle("test-auth-key");

        using var sent = JsonDocument.Parse(handler.LastBody!);
        sent.RootElement.GetProperty("sender").GetString().Should().Be("SCHERP");
        sent.RootElement.GetProperty("route").GetString().Should().Be("4", "transactional route");
        sent.RootElement.GetProperty("DLT_TE_ID").GetString().Should().Be("1207100000000000000");
        var sms = sent.RootElement.GetProperty("sms")[0];
        sms.GetProperty("message").GetString().Should().Be("Your ward was absent today.");
        sms.GetProperty("to")[0].GetString().Should().Be("919876501234", "no plus sign for MSG91");
    }

    [Theory]
    [InlineData("+919876501234", "919876501234")]
    [InlineData("9876501234", "919876501234")]
    [InlineData("91 98765 01234", "919876501234")]
    public void Phone_normalization_produces_country_prefixed_digits(string input, string expected) =>
        Msg91SmsSender.NormalizePhone(input).Should().Be(expected);

    [Fact]
    public async Task Provider_error_bodies_throw_so_the_outbox_retries()
    {
        var http200Error = CreateSender(new FakeHandler(
            HttpStatusCode.OK, """{"type":"error","message":"Invalid authkey"}"""));
        var act = () => http200Error.SendAsync("+919876501234", "x");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Invalid authkey*");

        var http401 = CreateSender(new FakeHandler(HttpStatusCode.Unauthorized, "{}"));
        var act2 = () => http401.SendAsync("+919876501234", "x");
        await act2.Should().ThrowAsync<InvalidOperationException>().WithMessage("*401*");
    }

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
