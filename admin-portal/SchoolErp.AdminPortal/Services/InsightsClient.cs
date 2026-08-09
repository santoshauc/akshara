using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the insights APIs.</summary>
public sealed class InsightsClient
{
    private readonly HttpClient _http;

    public InsightsClient(HttpClient http) => _http = http;

    /// <summary>Null for accounts without insights.view.</summary>
    public async Task<ManagementInsightsDto?> GetManagementAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/insights/management", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ManagementInsightsDto>(cancellationToken: ct)
            : null;
    }

    /// <summary>Peer comparison for one student (students.view); null when unavailable.</summary>
    public async Task<StudentInsightsDto?> GetStudentAsync(
        Guid studentId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/students/{studentId}/insights", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<StudentInsightsDto>(cancellationToken: ct)
            : null;
    }

    /// <summary>Null for accounts without insights.view.</summary>
    public async Task<List<TeacherInsightDto>?> GetTeachersAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/insights/teachers", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<TeacherInsightDto>>(cancellationToken: ct)
            : null;
    }
}
