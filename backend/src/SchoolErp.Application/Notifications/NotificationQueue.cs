using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Domain.Outbox;

namespace SchoolErp.Application.Notifications;

/// <summary>Payload of an outbox "push" message.</summary>
public sealed record PushPayload(string Token, string Title, string Body);

/// <summary>Delivers push notifications. Implemented in Infrastructure.</summary>
public interface IPushSender
{
    Task SendAsync(string token, string title, string body, CancellationToken ct = default);
}

/// <summary>
/// The one way guardian-facing events reach parents: an SMS row plus a push
/// row per registered device of that phone number. Runs inside the caller's
/// tenant scope and transaction — same guarantees as the outbox itself.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "It genuinely enqueues: every call appends outbox queue rows.")]
public static class NotificationQueue
{
    public static async Task QueueGuardianAsync(
        IApplicationDbContext db,
        Guid tenantId,
        string phone,
        string title,
        string message,
        CancellationToken ct)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            TenantId = tenantId,
            Type = OutboxMessageTypes.Sms,
            Payload = JsonSerializer.Serialize(new SmsPayload(phone, message)),
        });

        var tokens = await db.PushTokens.AsNoTracking()
            .Where(t => t.Phone == phone)
            .Select(t => t.Token)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var token in tokens)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = tenantId,
                Type = OutboxMessageTypes.Push,
                Payload = JsonSerializer.Serialize(new PushPayload(token, title, message)),
            });
        }
    }
}
