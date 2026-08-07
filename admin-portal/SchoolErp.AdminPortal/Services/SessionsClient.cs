using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the caller's own device sessions.</summary>
public sealed class SessionsClient
{
    private readonly HttpClient _http;

    public SessionsClient(HttpClient http) => _http = http;

    public async Task<List<SessionDto>> GetSessionsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<SessionDto>>("api/v1/auth/sessions", ct) ?? [];

    /// <summary>Returns false when the session was already gone.</summary>
    public async Task<bool> RevokeAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/auth/sessions/{sessionId}", ct);
        return response.IsSuccessStatusCode;
    }
}
