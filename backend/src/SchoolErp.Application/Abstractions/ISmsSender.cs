namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Outbound SMS gateway. Production implementations wrap DLT-registered Indian
/// providers (MSG91, Gupshup, …); development uses a logging sender.
/// </summary>
public interface ISmsSender
{
    /// <summary>Sends <paramref name="message"/> to an E.164 <paramref name="phone"/>.</summary>
    Task SendAsync(string phone, string message, CancellationToken ct = default);
}
