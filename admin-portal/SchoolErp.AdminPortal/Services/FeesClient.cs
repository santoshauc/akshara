using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;
using SchoolErp.Domain.Fees;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the fees APIs.</summary>
public sealed class FeesClient
{
    private readonly HttpClient _http;

    public FeesClient(HttpClient http) => _http = http;

    public async Task<List<FeeHeadDto>> GetHeadsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<FeeHeadDto>>("api/v1/fees/heads", ct) ?? [];

    public async Task<string?> CreateHeadAsync(
        string name, LateFineType lateFineType = LateFineType.None,
        decimal lateFineValue = 0, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/fees/heads", new { name, lateFineType, lateFineValue }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>The family ledger reached from any sibling; null when unavailable.</summary>
    public async Task<FamilyFeeSummaryDto?> GetFamilyAsync(
        Guid studentId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/fees/students/{studentId}/family", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<FamilyFeeSummaryDto>(cancellationToken: ct)
            : null;
    }

    /// <summary>Grants a concession; null on success.</summary>
    public async Task<string?> GrantConcessionAsync(
        GrantConcessionRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/fees/concessions", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Withdraws a concession; null on success.</summary>
    public async Task<string?> RevokeConcessionAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/v1/fees/concessions/{id}", ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Fetches a payment's receipt PDF; null when unavailable.</summary>
    public async Task<byte[]?> GetReceiptAsync(Guid paymentId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/fees/payments/{paymentId}/receipt", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(ct)
            : null;
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
