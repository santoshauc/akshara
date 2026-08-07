using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the exams APIs.</summary>
public sealed class ExamsClient
{
    private readonly HttpClient _http;

    public ExamsClient(HttpClient http) => _http = http;

    public async Task<List<SubjectDto>> GetSubjectsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<SubjectDto>>("api/v1/exams/subjects", ct) ?? [];

    public async Task<string?> CreateSubjectAsync(CreateSubjectRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/exams/subjects", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<ExamDto>> GetExamsAsync(Guid academicYearId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<ExamDto>>(
            $"api/v1/exams?academicYearId={academicYearId}", ct) ?? [];

    public async Task<string?> CreateExamAsync(CreateExamRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/exams", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> SchedulePaperAsync(
        Guid examId, SchedulePaperRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"api/v1/exams/{examId}/subjects", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public Task<MarksGridDto?> GetMarksGridAsync(Guid examSubjectId, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<MarksGridDto>($"api/v1/exams/papers/{examSubjectId}/marks", ct);

    public async Task<string?> EnterMarksAsync(
        Guid examSubjectId, EnterMarksRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"api/v1/exams/papers/{examSubjectId}/marks", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> PublishAsync(Guid examId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/exams/{examId}/publish", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<StudentResultDto?> GetStudentResultAsync(
        Guid examId, Guid studentId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/exams/{examId}/results/{studentId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null; // 404 = no marks for this exam
        }

        return await response.Content.ReadFromJsonAsync<StudentResultDto>(cancellationToken: ct);
    }
}
