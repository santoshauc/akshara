using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Notifications;

/// <summary>SMS provider selection and MSG91 credentials.</summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>"dev" (log only, default) or "msg91".</summary>
    public string Provider { get; set; } = "dev";

    public string Msg91AuthKey { get; set; } = string.Empty;

    /// <summary>Six-char DLT-approved sender id (e.g. "SCHERP").</summary>
    public string Msg91SenderId { get; set; } = string.Empty;

    /// <summary>
    /// DLT-registered template id sent with every message. Indian TRAI rules
    /// require the message text to match a registered template; per-message
    /// template mapping is future work — register a multi-variable template.
    /// </summary>
    public string Msg91DltTemplateId { get; set; } = string.Empty;
}

/// <summary>
/// MSG91 adapter over the sendsms v2 API (route 4 = transactional) with the
/// DLT template id attached. Failures throw so the outbox retries; the
/// dispatcher's attempt cap dead-letters poison messages.
/// </summary>
public sealed partial class Msg91SmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly SmsOptions _options;
    private readonly ILogger<Msg91SmsSender> _logger;

    public Msg91SmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<Msg91SmsSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        if (!_http.DefaultRequestHeaders.Contains("authkey"))
        {
            _http.DefaultRequestHeaders.Add("authkey", _options.Msg91AuthKey);
        }
    }

    public async Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        var payload = new Msg91Request(
            _options.Msg91SenderId,
            Route: "4",
            Country: "91",
            [new Msg91Sms(message, [NormalizePhone(phone)])],
            _options.Msg91DltTemplateId);

        var response = await _http.PostAsJsonAsync("api/v2/sendsms", payload, ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode ||
            body.Contains("\"type\":\"error\"", StringComparison.OrdinalIgnoreCase))
        {
            LogSendFailed(_logger, (int)response.StatusCode);
            throw new InvalidOperationException(
                $"MSG91 send failed ({(int)response.StatusCode}): {Truncate(body)}");
        }
    }

    /// <summary>MSG91 wants digits with the country code and no plus sign.</summary>
    public static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length == 10 ? $"91{digits}" : digits;
    }

    private static string Truncate(string value) =>
        value.Length <= 512 ? value : value[..512];

    private sealed record Msg91Request(
        [property: JsonPropertyName("sender")] string Sender,
        [property: JsonPropertyName("route")] string Route,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("sms")] IReadOnlyList<Msg91Sms> Sms,
        [property: JsonPropertyName("DLT_TE_ID")] string DltTemplateId);

    private sealed record Msg91Sms(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("to")] IReadOnlyList<string> To);

    [LoggerMessage(Level = LogLevel.Error, Message = "MSG91 send failed with HTTP {StatusCode}")]
    private static partial void LogSendFailed(ILogger logger, int statusCode);
}
