using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Notifications;

/// <summary>SMTP settings. Activated by <c>Email:Provider=smtp</c>.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"smtp" to send for real; anything else logs.</summary>
    public string Provider { get; set; } = "dev";

    public string Host { get; set; } = string.Empty;

    /// <summary>587 is the submission port; 465 implies implicit TLS.</summary>
    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Envelope sender. Must be a domain the provider lets you send as.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Display name; falls back to the address when blank.</summary>
    public string FromName { get; set; } = string.Empty;

    public bool UseTls { get; set; } = true;
}

/// <summary>Development email channel: logs instead of sending.</summary>
public sealed partial class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        LogEmail(_logger, to, subject, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[DEV EMAIL] to {To}: {Subject} — {Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}

/// <summary>
/// SMTP adapter over <see cref="SmtpClient"/>. Activated by
/// <c>Email:Provider=smtp</c>; until then <see cref="DevEmailSender"/> logs,
/// mirroring how MSG91, Meta and Expo switch on.
/// <para>
/// SmtpClient is marked obsolete by Microsoft in favour of MailKit for
/// FEATURE-RICH clients, and the guidance is about OAuth, modern auth and
/// protocol coverage. What this sends is a plain-text notice over authenticated
/// submission to one configured relay, which SmtpClient does correctly and
/// without adding a dependency. Revisit if a provider ever requires XOAUTH2.
/// </para>
/// <para>
/// Failures are allowed to throw: the outbox counts the attempt and retries,
/// which is the same contract every other sender here honours.
/// </para>
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException(
                "Email:Provider is 'smtp' but Email:Host is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException(
                "Email:Provider is 'smtp' but Email:FromAddress is not configured.");
        }
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        // An empty username means an unauthenticated relay - normal for an
        // internal mail host, and passing blank credentials would fail it.
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        using var mail = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(_options.FromName)
                ? new MailAddress(_options.FromAddress)
                : new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        mail.To.Add(to);

        // SendMailAsync ignores a CancellationToken on this client, so the token
        // is honoured by checking before the call rather than pretending.
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail, ct).ConfigureAwait(false);
    }
}
