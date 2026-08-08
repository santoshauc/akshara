using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SchoolErp.Application.Notifications;

namespace SchoolErp.Infrastructure.Notifications;

/// <summary>Development push channel: logs instead of sending.</summary>
public sealed partial class DevPushSender : IPushSender
{
    private readonly ILogger<DevPushSender> _logger;

    public DevPushSender(ILogger<DevPushSender> logger) => _logger = logger;

    public Task SendAsync(string token, string title, string body, CancellationToken ct = default)
    {
        LogPush(_logger, token, title, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[DEV PUSH] to {Token}: {Title} — {Body}")]
    private static partial void LogPush(ILogger logger, string token, string title, string body);
}

/// <summary>
/// Expo push service adapter (https://docs.expo.dev/push-notifications/sending-notifications/).
/// Activated by Push:Provider=expo; no credentials required for the public
/// endpoint. Delivery errors surface as exceptions so the outbox retries.
/// </summary>
public sealed class ExpoPushSender : IPushSender
{
    private readonly HttpClient _http;

    public ExpoPushSender(HttpClient http) => _http = http;

    public async Task SendAsync(string token, string title, string body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/--/api/v2/push/send", new
        {
            to = token,
            title,
            body,
            sound = "default",
        }, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Expo returns 200 with a per-message status; a rejected ticket means
        // the token is dead or malformed — surface it for the retry counter.
        var ticket = await response.Content.ReadFromJsonAsync<ExpoPushResponse>(ct)
            .ConfigureAwait(false);
        var status = ticket?.Data?.FirstOrDefault()?.Status;
        if (status is not null && status != "ok")
        {
            throw new InvalidOperationException(
                $"Expo push rejected ({ticket!.Data![0].Message ?? status}).");
        }
    }

    private sealed record ExpoPushResponse(List<ExpoPushTicket>? Data);

    private sealed record ExpoPushTicket(string? Status, string? Message);
}
