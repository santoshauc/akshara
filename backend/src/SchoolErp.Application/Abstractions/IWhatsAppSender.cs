namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Outbound WhatsApp channel. Same shape as <see cref="ISmsSender"/> so the
/// outbox can route the one payload to either channel per school preference.
/// </summary>
public interface IWhatsAppSender
{
    /// <summary>Sends <paramref name="message"/> to an E.164 <paramref name="phone"/>.</summary>
    Task SendAsync(string phone, string message, CancellationToken ct = default);
}
