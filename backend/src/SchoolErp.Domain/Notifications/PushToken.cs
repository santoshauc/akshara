using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.Notifications;

/// <summary>
/// One device's Expo push token. Phone is denormalized from the owning user
/// at registration so guardian-facing events (queued inside a tenant scope by
/// guardian phone) can fan out to devices without touching the identity store.
/// </summary>
public class PushToken : TenantEntity
{
    public Guid UserId { get; set; }

    /// <summary>The owning user's phone (E.164) at registration time.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Expo push token (e.g. "ExponentPushToken[xxxx]").</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>"android" | "ios" | "web".</summary>
    public string Platform { get; set; } = string.Empty;
}
