using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;
using SchoolErp.Domain.Admissions;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the admissions enquiry APIs.</summary>
public sealed class AdmissionsClient
{
    private readonly HttpClient _http;

    public AdmissionsClient(HttpClient http) => _http = http;

    public async Task<List<EnquiryDto>> GetEnquiriesAsync(
        EnquiryStatus? status = null, CancellationToken ct = default)
    {
        var query = status is null ? "" : $"?status={status}";
        return await _http.GetFromJsonAsync<List<EnquiryDto>>(
            $"api/v1/admissions/enquiries{query}", ct) ?? [];
    }

    /// <summary>Registers a fresh enquiry; null on success.</summary>
    public async Task<string?> CreateAsync(
        CreateEnquiryRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/admissions/enquiries", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Moves an enquiry through the pipeline; null on success.</summary>
    public async Task<string?> UpdateAsync(
        Guid id, EnquiryStatus status, DateOnly? followUpOn, string? notes,
        CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/admissions/enquiries/{id}", new { status, followUpOn, notes }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Stamps the enquiry Admitted with the created student; null on success.</summary>
    public async Task<string?> ConvertAsync(
        Guid id, Guid studentId, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/admissions/enquiries/{id}/convert", new { studentId }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
