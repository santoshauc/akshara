using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the front-desk APIs.</summary>
public sealed class FrontOfficeClient
{
    private readonly HttpClient _http;

    public FrontOfficeClient(HttpClient http) => _http = http;

    /// <summary>A day's register, or everyone still on the premises.</summary>
    public async Task<List<VisitorEntryDto>> GetVisitorsAsync(
        DateOnly? date = null, bool openOnly = false, CancellationToken ct = default)
    {
        var query = openOnly
            ? "?openOnly=true"
            : date is { } day ? $"?date={day:yyyy-MM-dd}" : "";
        return await _http.GetFromJsonAsync<List<VisitorEntryDto>>(
            $"api/v1/front-office/visitors{query}", ct) ?? [];
    }

    public async Task<string?> CheckInAsync(
        CheckInVisitorRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/front-office/visitors", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> CheckOutAsync(Guid visitorEntryId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"api/v1/front-office/visitors/{visitorEntryId}/check-out", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<GatePassDto>> GetGatePassesAsync(
        DateOnly? date = null, CancellationToken ct = default)
    {
        var query = date is { } day ? $"?date={day:yyyy-MM-dd}" : "";
        return await _http.GetFromJsonAsync<List<GatePassDto>>(
            $"api/v1/front-office/gate-passes{query}", ct) ?? [];
    }

    public async Task<string?> IssueGatePassAsync(
        IssueGatePassRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/front-office/gate-passes", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> MarkReturnedAsync(Guid gatePassId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"api/v1/front-office/gate-passes/{gatePassId}/returned", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
