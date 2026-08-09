using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>
/// Platform-operator endpoints. Everything here is [PlatformOnly] server-side
/// AND refused while the caller has not enabled MFA, so a 403 from these calls
/// usually means "turn MFA on", not "you are not an operator".
/// </summary>
public sealed class PlatformClient
{
    private readonly HttpClient _http;

    public PlatformClient(HttpClient http) => _http = http;

    public async Task<List<PlatformOperatorDto>> GetOperatorsAsync(
        CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<PlatformOperatorDto>>(
            "api/v1/platform/operators", ct) ?? [];

    /// <summary>Null on success, else the server's explanation.</summary>
    public async Task<string?> CreateOperatorAsync(
        string fullName, string email, string temporaryPassword, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/platform/operators",
            new { fullName, email, temporaryPassword }, ct);
        return response.IsSuccessStatusCode
            ? null
            : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> SetOperatorActiveAsync(
        Guid operatorId, bool isActive, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/platform/operators/{operatorId}/active", new { isActive }, ct);
        return response.IsSuccessStatusCode
            ? null
            : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> ResetOperatorPasswordAsync(
        Guid operatorId, string newPassword, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(
            $"api/v1/platform/operators/{operatorId}/password", new { newPassword }, ct);
        return response.IsSuccessStatusCode
            ? null
            : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Operator actions, or one school's trail when schoolId is given.</summary>
    public async Task<List<AuditEventDto>> GetPlatformAuditAsync(
        string? search, Guid? schoolId, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (schoolId is { } id)
        {
            query.Add($"schoolId={id}");
        }

        var url = "api/v1/platform/audit" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await _http.GetFromJsonAsync<List<AuditEventDto>>(url, ct) ?? [];
    }
}
