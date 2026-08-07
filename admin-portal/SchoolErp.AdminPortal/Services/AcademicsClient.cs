using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for academic-structure APIs.</summary>
public sealed class AcademicsClient
{
    private readonly HttpClient _http;

    public AcademicsClient(HttpClient http) => _http = http;

    public async Task<List<AcademicYearDto>> GetYearsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<AcademicYearDto>>("api/v1/academics/years", ct) ?? [];

    public async Task<string?> CreateYearAsync(CreateAcademicYearRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/academics/years", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> SetCurrentYearAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/academics/years/{id}/set-current", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<SchoolClassDto>> GetClassesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<SchoolClassDto>>("api/v1/academics/classes", ct) ?? [];

    public async Task<string?> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/academics/classes", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
