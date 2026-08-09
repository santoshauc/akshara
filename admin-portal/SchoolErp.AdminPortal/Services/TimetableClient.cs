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

    public async Task<List<SubstitutionSlotDto>> GetSubstitutionPlanAsync(
        Guid teacherId, DateOnly date, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<SubstitutionSlotDto>>(
            $"api/v1/timetable/substitutions/plan?teacherId={teacherId}&date={date:yyyy-MM-dd}",
            ct) ?? [];

    /// <summary>Publishes the day's covers; null on success.</summary>
    public async Task<string?> ApplySubstitutionsAsync(
        Guid absentTeacherId, DateOnly date, List<SubstitutionInput> items,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/timetable/substitutions",
            new { absentTeacherId, date, items }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<SubstitutionDto>> GetSubstitutionsAsync(
        DateOnly date, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<SubstitutionDto>>(
            $"api/v1/timetable/substitutions?date={date:yyyy-MM-dd}", ct) ?? [];
}
