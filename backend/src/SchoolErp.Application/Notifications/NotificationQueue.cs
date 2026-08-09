using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Domain.Outbox;
using SchoolErp.Shared.Localization;

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
/// <para>
/// Callers pass a template key, never finished prose: the text is rendered
/// here in the recipient's own language (<c>Guardian.PreferredLanguage</c>,
/// looked up by phone within the tenant). Because rendering happens at queue
/// time, SMS, the WhatsApp route and push all carry the same localized copy.
/// </para>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "It genuinely enqueues: every call appends outbox queue rows.")]
public static class NotificationQueue
{
    public static async Task QueueGuardianAsync(
        IApplicationDbContext db,
        Guid tenantId,
        string phone,
        string templateKey,
        object?[] args,
        CancellationToken ct)
    {
        var language = await ResolveLanguageAsync(db, phone, ct).ConfigureAwait(false);
        var (title, message) = NotificationStrings.RenderMessage(language, templateKey, args);

        db.OutboxMessages.Add(new OutboxMessage
        {
            TenantId = tenantId,
            Type = OutboxMessageTypes.Sms,
            Payload = JsonSerializer.Serialize(new SmsPayload(phone, message, templateKey)),
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

    /// <summary>
    /// The guardian's language, found by phone inside the current tenant scope.
    /// Several guardian rows can share a phone (siblings admitted separately);
    /// any of them answers, since the preference belongs to the person.
    /// </summary>
    public static async Task<string> ResolveLanguageAsync(
        IApplicationDbContext db, string phone, CancellationToken ct)
    {
        var stored = await db.Guardians.AsNoTracking()
            .Where(g => g.Phone == phone)
            .Select(g => g.PreferredLanguage)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return NotificationLanguages.Normalize(stored);
    }
}
