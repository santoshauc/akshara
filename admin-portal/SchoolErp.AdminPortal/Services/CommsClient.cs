using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for notices and homework APIs.</summary>
public sealed class CommsClient
{
    private readonly HttpClient _http;

    public CommsClient(HttpClient http) => _http = http;

    public async Task<List<NoticeDto>> GetNoticesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<NoticeDto>>("api/v1/notices", ct) ?? [];

    public async Task<string?> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/notices", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<HomeworkDto>> GetClassHomeworkAsync(Guid classId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<HomeworkDto>>($"api/v1/homework?classId={classId}", ct) ?? [];

    public async Task<string?> CreateHomeworkAsync(CreateHomeworkRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/homework", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
