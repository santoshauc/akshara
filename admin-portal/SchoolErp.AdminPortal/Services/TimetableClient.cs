using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the timetable APIs.</summary>
public sealed class TimetableClient
{
    private readonly HttpClient _http;

    public TimetableClient(HttpClient http) => _http = http;

    public async Task<List<TimetableEntryDto>> GetAsync(
        Guid classId, Guid? sectionId, CancellationToken ct = default)
    {
        var query = $"classId={classId}" + (sectionId is { } s ? $"&sectionId={s}" : "");
        return await _http.GetFromJsonAsync<List<TimetableEntryDto>>(
            $"api/v1/timetable?{query}", ct) ?? [];
    }

    public async Task<string?> DefineAsync(DefineTimetableRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("api/v1/timetable", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> PublishAsync(PublishTimetableRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/timetable/publish", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
