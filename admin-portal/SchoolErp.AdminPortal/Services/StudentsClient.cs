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

    /// <summary>Uploads a student photo; returns the served URL or the problem message.</summary>
    public async Task<(string? PhotoUrl, string? Error)> UploadPhotoAsync(
        Guid id, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var file = new StreamContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        var response = await _http.PostAsync($"api/v1/students/{id}/photo", form, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<string>(cancellationToken: ct), null);
        }

        return (null, await ProblemResponse.ReadTitleAsync(response, ct));
    }

    /// <summary>Fetches an official document PDF (transfer-certificate,
    /// bonafide-certificate or id-card); null when unavailable.</summary>
    public async Task<byte[]?> GetDocumentAsync(
        Guid id, string type, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/students/{id}/documents/{type}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(ct)
            : null;
    }

    /// <summary>DPDP data export as JSON bytes; null when unavailable.</summary>
    public async Task<byte[]?> ExportDataAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/students/{id}/data-export", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(ct)
            : null;
    }

    /// <summary>DPDP erasure; null on success.</summary>
    public async Task<string?> EraseDataAsync(
        Guid id, string reason, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/students/{id}/erase", new { reason }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Turns a server-relative file URL into an absolute one on the API host.</summary>
    public string? FileUrl(string? relativeUrl) =>
        relativeUrl is null ? null : new Uri(_http.BaseAddress!, relativeUrl).ToString();
}
