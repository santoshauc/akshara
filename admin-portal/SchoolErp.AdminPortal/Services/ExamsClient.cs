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

    /// <summary>
    /// The cumulative grade sheet. Null only when the call fails; a student
    /// with no published results still returns a sheet that says so.
    /// </summary>
    public async Task<GradeSheetDto?> GetGradeSheetAsync(
        Guid studentId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"api/v1/exams/students/{studentId}/grade-sheet", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<GradeSheetDto>(cancellationToken: ct)
            : null;
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

    public async Task<List<TermReportDto>> GetTermReportsAsync(
        Guid academicYearId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<TermReportDto>>(
            $"api/v1/exams/term-reports?academicYearId={academicYearId}", ct) ?? [];

    /// <summary>Creates a term report definition; null on success.</summary>
    public async Task<string?> CreateTermReportAsync(
        Guid academicYearId, string name,
        List<TermReportComponentInput> components, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/exams/term-reports",
            new { academicYearId, name, components }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Saves a student's co-scholastic grades + remarks; null on success.</summary>
    public async Task<string?> SetTermStudentInputAsync(
        Guid termReportId, Guid studentId,
        Dictionary<string, string> coScholastic, string? remarks, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/exams/term-reports/{termReportId}/students/{studentId}",
            new { coScholastic, remarks }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Fetches a student's term report PDF; null when unavailable.</summary>
    public async Task<byte[]?> GetTermReportPdfAsync(
        Guid termReportId, Guid studentId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"api/v1/exams/term-reports/{termReportId}/students/{studentId}/pdf", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(ct)
            : null;
    }

    /// <summary>This school's report-card layout settings.</summary>
    public async Task<ReportCardSettingsDto?> GetReportCardSettingsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/exams/report-card-settings", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ReportCardSettingsDto>(cancellationToken: ct)
            : null;
    }

    /// <summary>Saves them; null on success.</summary>
    public async Task<string?> UpdateReportCardSettingsAsync(
        ReportCardSettingsDto settings, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            "api/v1/exams/report-card-settings", settings, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}