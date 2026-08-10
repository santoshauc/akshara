using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for a college's departments and programmes.</summary>
public sealed class DepartmentsClient
{
    private readonly HttpClient _http;

    public DepartmentsClient(HttpClient http) => _http = http;

    public async Task<List<DepartmentDto>> GetDepartmentsAsync(
        bool includeClosed = false, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<DepartmentDto>>(
            $"api/v1/departments?includeClosed={includeClosed}", ct) ?? [];

    /// <summary>Null on success, else the server's explanation.</summary>
    public async Task<string?> CreateDepartmentAsync(
        CreateDepartmentRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/departments", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> UpdateDepartmentAsync(
        Guid id, UpdateDepartmentRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/departments/{id}", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> CreateProgrammeAsync(
        CreateProgrammeRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/departments/programmes", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> UpdateProgrammeAsync(
        Guid id, UpdateProgrammeRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/departments/programmes/{id}", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
