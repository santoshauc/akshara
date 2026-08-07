using Microsoft.Extensions.Logging;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Infrastructure.Identity;

/// <summary>
/// Development SMS sender: writes the message to the log instead of a gateway.
/// Replaced by a DLT-registered provider adapter (MSG91/Gupshup) in production.
/// </summary>
public sealed partial class DevSmsSender : ISmsSender
{
    private readonly ILogger<DevSmsSender> _logger;

    public DevSmsSender(ILogger<DevSmsSender> logger) => _logger = logger;

    public Task SendAsync(string phone, string message, CancellationToken ct = default)
    {
        LogSms(_logger, phone, message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[DEV SMS] to {Phone}: {Message}")]
    private static partial void LogSms(ILogger logger, string phone, string message);
}
