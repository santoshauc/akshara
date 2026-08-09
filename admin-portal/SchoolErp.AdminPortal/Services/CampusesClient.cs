using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the campus register.</summary>
public sealed class CampusesClient
{
    private readonly HttpClient _http;

    public CampusesClient(HttpClient http) => _http = http;

    public async Task<List<CampusDto>> GetCampusesAsync(
        bool includeInactive = false, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<CampusDto>>(
            $"api/v1/campuses?includeInactive={includeInactive}", ct) ?? [];

    /// <summary>Adds a campus; null on success, otherwise the reason.</summary>
    public async Task<string?> CreateAsync(CreateCampusRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/campuses", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Edits a campus; null on success, otherwise the reason.</summary>
    public async Task<string?> UpdateAsync(
        Guid id, UpdateCampusRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/campuses/{id}", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Moves the head-campus flag; null on success.</summary>
    public async Task<string?> SetPrimaryAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PutAsync($"api/v1/campuses/{id}/primary", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
