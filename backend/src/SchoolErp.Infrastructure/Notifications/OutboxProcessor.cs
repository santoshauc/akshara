using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Domain.Outbox;
using SchoolErp.Infrastructure.Persistence;

namespace SchoolErp.Infrastructure.Notifications;

/// <summary>
/// Delivers pending outbox messages. Separated from the hosting loop so tests
/// can drive it synchronously. Failures increment <see cref="OutboxMessage.Attempts"/>;
/// after 5 the row is left for operator inspection (dead-letter by flag).
/// </summary>
public sealed partial class OutboxProcessor
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private readonly AppDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        AppDbContext db,
        ISmsSender smsSender,
        TimeProvider clock,
        ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _smsSender = smsSender;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Processes one batch; returns how many messages were handled.</summary>
    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        var pending = await _db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var message in pending)
        {
            try
            {
                await DeliverAsync(message, ct).ConfigureAwait(false);
                message.ProcessedAt = _clock.GetUtcNow();
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 1024 ? ex.Message[..1024] : ex.Message;
                LogDeliveryFailed(_logger, ex, message.Id, message.Attempts);
            }
        }

        if (pending.Count > 0)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return pending.Count;
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken ct)
    {
        switch (message.Type)
        {
            case OutboxMessageTypes.Sms:
                var sms = JsonSerializer.Deserialize<SmsPayload>(message.Payload)
                    ?? throw new InvalidOperationException("Empty SMS payload.");
                await _smsSender.SendAsync(sms.Phone, sms.Message, ct).ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unknown outbox message type '{message.Type}'.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox delivery failed for message {MessageId} (attempt {Attempt})")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception ex, Guid messageId, int attempt);
}
