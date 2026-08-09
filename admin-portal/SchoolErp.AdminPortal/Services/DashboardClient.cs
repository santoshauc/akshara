using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the dashboard tiles.</summary>
public sealed class DashboardClient
{
    private readonly HttpClient _http;

    public DashboardClient(HttpClient http) => _http = http;

    /// <summary>Null for accounts without students.view (platform sign-ins).</summary>
    public async Task<DashboardDto?> GetAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/dashboard", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<DashboardDto>(cancellationToken: ct)
            : null;
    }
}
