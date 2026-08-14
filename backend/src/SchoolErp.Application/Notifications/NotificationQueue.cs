using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Domain.Outbox;
using SchoolErp.Shared.Localization;

namespace SchoolErp.Application.Notifications;

/// <summary>Payload of an outbox "push" message.</summary>
public sealed record PushPayload(string Token, string Title, string Body);

/// <summary>
/// Payload of an outbox "email" message. <c>Template</c> mirrors
/// <see cref="SchoolErp.Application.Attendance.SmsPayload"/>: it is the marker
/// jobs match on when they need to find their own earlier messages, and it must
/// not be inferred from the rendered text — that text changes with the reader's
/// language, so matching on prose stops working the moment someone switches to
/// Telugu.
/// </summary>
public sealed record EmailPayload(string To, string Subject, string Body, string? Template = null);

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
        var contact = await ResolveContactAsync(db, phone, ct).ConfigureAwait(false);
        var (title, message) = NotificationStrings.RenderMessage(contact.Language, templateKey, args);

        db.OutboxMessages.Add(new OutboxMessage
        {
            TenantId = tenantId,
            Type = OutboxMessageTypes.Sms,
            Payload = JsonSerializer.Serialize(new SmsPayload(phone, message, templateKey)),
        });

        // Email only when the school actually holds an address. Most guardians in
        // this market are reachable by phone and nothing else, so queueing a row
        // per event regardless would fill the outbox with messages that can only
        // ever dead-letter. The rendered copy is the SAME text in the SAME
        // language as the SMS - the whole point of rendering once, here.
        if (!string.IsNullOrWhiteSpace(contact.Email))
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = tenantId,
                Type = OutboxMessageTypes.Email,
                Payload = JsonSerializer.Serialize(
                    new EmailPayload(contact.Email, title, message, templateKey)),
            });
        }

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
        IApplicationDbContext db, string phone, CancellationToken ct) =>
        (await ResolveContactAsync(db, phone, ct).ConfigureAwait(false)).Language;

    /// <summary>
    /// How to reach this guardian, and in which language — one query rather than
    /// one per channel. Ordered so a row that HAS an email wins: sibling
    /// admissions can leave several rows on the same phone, and only some of
    /// them may carry an address.
    /// </summary>
    private static async Task<GuardianContact> ResolveContactAsync(
        IApplicationDbContext db, string phone, CancellationToken ct)
    {
        var rows = await db.Guardians.AsNoTracking()
            .Where(g => g.Phone == phone)
            .Select(g => new { g.PreferredLanguage, g.Email })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var preferred = rows.Find(r => !string.IsNullOrWhiteSpace(r.Email)) ?? rows.FirstOrDefault();

        return new GuardianContact(
            NotificationLanguages.Normalize(preferred?.PreferredLanguage),
            string.IsNullOrWhiteSpace(preferred?.Email) ? null : preferred.Email.Trim());
    }

    private sealed record GuardianContact(string Language, string? Email);
}
