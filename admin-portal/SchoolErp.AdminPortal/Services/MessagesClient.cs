using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for parent↔school messaging.</summary>
public sealed class MessagesClient
{
    private readonly HttpClient _http;

    public MessagesClient(HttpClient http) => _http = http;

    public async Task<List<MessageThreadDto>> GetThreadsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<MessageThreadDto>>("api/v1/messages/threads", ct) ?? [];

    public async Task<List<StudentMessageDto>> GetConversationAsync(
        Guid studentId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<StudentMessageDto>>(
            $"api/v1/messages/students/{studentId}", ct) ?? [];

    /// <summary>Sends a staff reply; null on success.</summary>
    public async Task<string?> SendAsync(
        Guid studentId, string body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/messages/students/{studentId}", new { body }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
