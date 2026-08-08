using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the leave APIs.</summary>
public sealed class LeaveClient
{
    private readonly HttpClient _http;

    public LeaveClient(HttpClient http) => _http = http;

    public async Task<List<LeaveRequestDto>> GetRequestsAsync(
        string? status = null, CancellationToken ct = default)
    {
        var query = status is null ? "" : $"?status={status}";
        return await _http.GetFromJsonAsync<List<LeaveRequestDto>>(
            $"api/v1/leave{query}", ct) ?? [];
    }

    /// <summary>Approve or reject; null on success.</summary>
    public async Task<string?> DecideAsync(
        Guid id, bool approve, string? note, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/leave/{id}/decision", new { approve, note }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<LeaveRequestDto>> GetMineAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<LeaveRequestDto>>("api/v1/leave/mine", ct) ?? [];

    /// <summary>Files the caller's own leave; null on success.</summary>
    public async Task<string?> SubmitMineAsync(
        DateOnly fromDate, DateOnly toDate, string reason, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/leave/mine", new { fromDate, toDate, reason }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
