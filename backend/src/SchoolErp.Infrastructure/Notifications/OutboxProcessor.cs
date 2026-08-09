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
    private readonly IWhatsAppSender _whatsAppSender;
    private readonly Application.Notifications.IPushSender _pushSender;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        AppDbContext db,
        ISmsSender smsSender,
        IWhatsAppSender whatsAppSender,
        Application.Notifications.IPushSender pushSender,
        TimeProvider clock,
        ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _smsSender = smsSender;
        _whatsAppSender = whatsAppSender;
        _pushSender = pushSender;
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

        // One flag lookup per tenant per batch, not per message.
        var whatsAppTenants = await ResolveWhatsAppTenantsAsync(pending, ct).ConfigureAwait(false);

        foreach (var message in pending)
        {
            try
            {
                if (message.Type == OutboxMessageTypes.Sms &&
                    whatsAppTenants.Contains(message.TenantId) &&
                    await TryDeliverViaWhatsAppAsync(message, ct).ConfigureAwait(false))
                {
                    // Delivered on WhatsApp — no SMS credit spent.
                    message.ProcessedAt = _clock.GetUtcNow();
                    message.LastError = null;
                    continue;
                }

                if (message.Type == OutboxMessageTypes.Sms &&
                    !await TryConsumeSmsCreditAsync(message.TenantId, ct).ConfigureAwait(false))
                {
                    // Out of credits: dead-letter immediately (no point retrying —
                    // the queue would clog while the school tops up).
                    message.Attempts = MaxAttempts;
                    message.LastError = "No SMS credits remaining for this school.";
                    LogSmsCreditsExhausted(_logger, message.TenantId, message.Id);
                    continue;
                }

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

    /// <summary>Tenants in this batch that prefer WhatsApp for parent messages.</summary>
    private async Task<HashSet<Guid>> ResolveWhatsAppTenantsAsync(
        IReadOnlyList<OutboxMessage> pending, CancellationToken ct)
    {
        var tenantIds = pending
            .Where(m => m.Type == OutboxMessageTypes.Sms && m.TenantId != Guid.Empty)
            .Select(m => m.TenantId)
            .Distinct()
            .ToList();
        if (tenantIds.Count == 0)
        {
            return [];
        }

        return (await _db.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => tenantIds.Contains(t.Id) && t.WhatsAppEnabled)
                .Select(t => t.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false))
            .ToHashSet();
    }

    /// <summary>
    /// WhatsApp is best-effort: a failure logs and returns false so the same
    /// message falls back to SMS in the same pass (parents still get notified;
    /// the school just pays SMS rates for that one).
    /// </summary>
    private async Task<bool> TryDeliverViaWhatsAppAsync(OutboxMessage message, CancellationToken ct)
    {
        var sms = JsonSerializer.Deserialize<SmsPayload>(message.Payload)
            ?? throw new InvalidOperationException("Empty SMS payload.");
        try
        {
            await _whatsAppSender.SendAsync(sms.Phone, sms.Message, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWhatsAppFellBack(_logger, ex, message.Id);
            return false;
        }
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

            case OutboxMessageTypes.Push:
                var push = JsonSerializer
                    .Deserialize<Application.Notifications.PushPayload>(message.Payload)
                    ?? throw new InvalidOperationException("Empty push payload.");
                await _pushSender.SendAsync(push.Token, push.Title, push.Body, ct)
                    .ConfigureAwait(false);
                break;

            default:
                throw new InvalidOperationException($"Unknown outbox message type '{message.Type}'.");
        }
    }

    /// <summary>
    /// Atomically spends one SMS credit; returns false when the balance is 0.
    /// Platform messages (no tenant) are unmetered. The guarded UPDATE makes
    /// concurrent dispatchers safe — no read-modify-write race.
    /// </summary>
    private async Task<bool> TryConsumeSmsCreditAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return true;
        }

        var spent = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId && t.SmsCredits > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SmsCredits, t => t.SmsCredits - 1), ct)
            .ConfigureAwait(false);
        return spent > 0;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox delivery failed for message {MessageId} (attempt {Attempt})")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception ex, Guid messageId, int attempt);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SMS blocked: tenant {TenantId} has no SMS credits (message {MessageId} dead-lettered)")]
    private static partial void LogSmsCreditsExhausted(ILogger logger, Guid tenantId, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WhatsApp send failed for message {MessageId}; falling back to SMS")]
    private static partial void LogWhatsAppFellBack(ILogger logger, Exception ex, Guid messageId);
}
