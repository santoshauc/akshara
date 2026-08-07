using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;
using SchoolErp.Domain.Students;
using SchoolErp.Shared.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the student APIs.</summary>
public sealed class StudentsClient
{
    private readonly HttpClient _http;

    public StudentsClient(HttpClient http) => _http = http;

    public async Task<PagedResult<StudentListItemDto>> GetStudentsAsync(
        string? search,
        Guid? classId,
        Guid? sectionId,
        StudentStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }
        if (classId is { } c)
        {
            query.Add($"classId={c}");
        }
        if (sectionId is { } s)
        {
            query.Add($"sectionId={s}");
        }
        if (status is { } st)
        {
            query.Add($"status={st}");
        }

        return (await _http.GetFromJsonAsync<PagedResult<StudentListItemDto>>(
            $"api/v1/students?{string.Join('&', query)}", ct))!;
    }

    public Task<StudentDetailDto?> GetStudentAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<StudentDetailDto>($"api/v1/students/{id}", ct);

    /// <summary>Admits a student; returns the new id or the problem message.</summary>
    public async Task<(Guid? Id, string? Error)> AdmitAsync(
        AdmitStudentRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/students", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: ct), null);
        }

        return (null, await ProblemResponse.ReadTitleAsync(response, ct));
    }
}
