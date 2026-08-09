using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Notifications;

/// <summary>WhatsApp provider selection and Meta Cloud API credentials.</summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>"dev" (log only, default) or "meta".</summary>
    public string Provider { get; set; } = "dev";

    /// <summary>The business phone-number id from the Meta app dashboard.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Permanent system-user access token with whatsapp_business_messaging.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Approved template with ONE body variable ({{1}} carries the message).
    /// Business-initiated messages outside the 24-hour service window must use
    /// a template, and school notifications are almost always business-initiated,
    /// so the template path is the default. Leave empty to send free-form text
    /// (dev/testing inside the service window only).
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Template language code registered with Meta (e.g. "en", "te").</summary>
    public string TemplateLanguage { get; set; } = "en";
}

/// <summary>
/// Meta WhatsApp Cloud API adapter (graph.facebook.com v21.0). Failures throw
/// so the outbox retries and the SMS fallback can take over; the dispatcher's
/// attempt cap dead-letters poison messages.
/// </summary>
public sealed partial class MetaWhatsAppSender : IWhatsAppSender
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<MetaWhatsAppSender> _logger;

    public MetaWhatsAppSender(
        HttpClient http, IOptions<WhatsAppOptions> options, ILogger<MetaWhatsAppSender> logger)
    {
        _http = http;
        _options = options.Value;
        if (_http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);
        }

        _logger = logger;
    }

    public async Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        var to = Msg91SmsSender.NormalizePhone(phone);
        object payload = string.IsNullOrWhiteSpace(_options.TemplateName)
            ? new TextRequest(to, new TextBody(message))
            : new TemplateRequest(to, new Template(
                _options.TemplateName,
                new TemplateLanguage(_options.TemplateLanguage),
                [new TemplateComponent("body", [new TemplateParameter("text", message)])]));

        var response = await _http
            .PostAsJsonAsync($"v21.0/{_options.PhoneNumberId}/messages", payload, ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            LogSendFailed(_logger, (int)response.StatusCode);
            throw new InvalidOperationException(
                $"WhatsApp send failed ({(int)response.StatusCode}): " +
                (body.Length <= 512 ? body : body[..512]));
        }
    }

    private sealed record TextRequest(
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("text")] TextBody Text,
        [property: JsonPropertyName("messaging_product")] string MessagingProduct = "whatsapp",
        [property: JsonPropertyName("type")] string Type = "text");

    private sealed record TextBody([property: JsonPropertyName("body")] string Body);

    private sealed record TemplateRequest(
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("template")] Template Template,
        [property: JsonPropertyName("messaging_product")] string MessagingProduct = "whatsapp",
        [property: JsonPropertyName("type")] string Type = "template");

    private sealed record Template(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("language")] TemplateLanguage Language,
        [property: JsonPropertyName("components")] IReadOnlyList<TemplateComponent> Components);

    private sealed record TemplateLanguage([property: JsonPropertyName("code")] string Code);

    private sealed record TemplateComponent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("parameters")] IReadOnlyList<TemplateParameter> Parameters);

    private sealed record TemplateParameter(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    [LoggerMessage(Level = LogLevel.Error, Message = "WhatsApp send failed with HTTP {StatusCode}")]
    private static partial void LogSendFailed(ILogger logger, int statusCode);
}

/// <summary>Logs instead of sending — the default until WhatsApp:Provider=meta.</summary>
public sealed partial class DevWhatsAppSender : IWhatsAppSender
{
    private readonly ILogger<DevWhatsAppSender> _logger;

    public DevWhatsAppSender(ILogger<DevWhatsAppSender> logger) => _logger = logger;

    public Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        LogWhatsApp(_logger, phone, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[DEV WHATSAPP] to {Phone}: {Message}")]
    private static partial void LogWhatsApp(ILogger logger, string phone, string message);
}
