using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the audit-trail API.</summary>
public sealed class AuditClient
{
    private readonly HttpClient _http;

    public AuditClient(HttpClient http) => _http = http;

    public async Task<List<AuditEventDto>> GetTrailAsync(
        string? search, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            parts.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (from is { } f)
        {
            parts.Add($"from={f:yyyy-MM-dd}");
        }

        if (to is { } t)
        {
            parts.Add($"to={t:yyyy-MM-dd}");
        }

        var query = parts.Count > 0 ? $"?{string.Join('&', parts)}" : "";
        return await _http.GetFromJsonAsync<List<AuditEventDto>>($"api/v1/audit{query}", ct) ?? [];
    }
}
