using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the fees APIs.</summary>
public sealed class FeesClient
{
    private readonly HttpClient _http;

    public FeesClient(HttpClient http) => _http = http;

    public async Task<List<FeeHeadDto>> GetHeadsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<FeeHeadDto>>("api/v1/fees/heads", ct) ?? [];

    public async Task<string?> CreateHeadAsync(string name, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/fees/heads", new { name }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<FeeStructureItemDto>> GetStructureAsync(
        Guid academicYearId, Guid classId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<FeeStructureItemDto>>(
            $"api/v1/fees/structure?academicYearId={academicYearId}&classId={classId}", ct) ?? [];

    public async Task<string?> DefineStructureAsync(
        DefineFeeStructureRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("api/v1/fees/structure", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<StudentFeeSummaryDto?> GetStudentSummaryAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"api/v1/fees/students/{studentId}?academicYearId={academicYearId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null; // 404 = not enrolled in this year
        }

        return await response.Content.ReadFromJsonAsync<StudentFeeSummaryDto>(cancellationToken: ct);
    }

    public async Task<(PaymentReceiptDto? Receipt, string? Error)> RecordPaymentAsync(
        RecordPaymentRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/fees/payments", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<PaymentReceiptDto>(cancellationToken: ct), null);
        }

        return (null, await ProblemResponse.ReadTitleAsync(response, ct));
    }
}
