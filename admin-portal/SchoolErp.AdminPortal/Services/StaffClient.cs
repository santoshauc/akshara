using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the teaching-staff APIs.</summary>
public sealed class StaffClient
{
    private readonly HttpClient _http;

    public StaffClient(HttpClient http) => _http = http;

    public async Task<List<TeacherDto>> GetTeachersAsync(
        string? search = null, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(search)
            ? ""
            : $"?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<TeacherDto>>($"api/v1/teachers{query}", ct) ?? [];
    }

    public async Task<string?> CreateAsync(CreateTeacherRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/teachers", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> UpdateAsync(UpdateTeacherRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/teachers/{request.TeacherId}", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Creates the teacher's staff login; null on success.</summary>
    public async Task<string?> CreateLoginAsync(
        Guid teacherId, string temporaryPassword, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/teachers/{teacherId}/login", new { temporaryPassword }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<TeacherScheduleItemDto>> GetScheduleAsync(
        Guid teacherId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<TeacherScheduleItemDto>>(
            $"api/v1/teachers/{teacherId}/schedule", ct) ?? [];
}
