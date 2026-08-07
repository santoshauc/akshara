using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the attendance APIs.</summary>
public sealed class AttendanceClient
{
    private readonly HttpClient _http;

    public AttendanceClient(HttpClient http) => _http = http;

    public Task<SectionAttendanceDto?> GetSectionAsync(
        Guid sectionId, DateOnly date, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<SectionAttendanceDto>(
            $"api/v1/attendance/sections/{sectionId}?date={date:yyyy-MM-dd}", ct);

    public async Task<string?> MarkAsync(
        Guid sectionId, MarkAttendanceRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/attendance/sections/{sectionId}", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public Task<StudentMonthAttendanceDto?> GetStudentMonthAsync(
        Guid studentId, int year, int month, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<StudentMonthAttendanceDto>(
            $"api/v1/attendance/students/{studentId}?year={year}&month={month}", ct);
}
