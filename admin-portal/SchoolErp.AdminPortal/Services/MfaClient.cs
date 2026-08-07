using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the caller's own MFA settings.</summary>
public sealed class MfaClient
{
    private readonly HttpClient _http;

    public MfaClient(HttpClient http) => _http = http;

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default) =>
        (await _http.GetFromJsonAsync<MfaStatusDto>("api/v1/auth/mfa", ct))?.Enabled ?? false;

    public async Task<MfaEnrollmentDto?> EnrollAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync("api/v1/auth/mfa/enroll", null, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<MfaEnrollmentDto>(cancellationToken: ct)
            : null;
    }

    /// <summary>Returns the recovery codes, or null when the code was wrong.</summary>
    public async Task<MfaEnableResultDto?> EnableAsync(string code, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/auth/mfa/enable", new { code }, ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<MfaEnableResultDto>(cancellationToken: ct)
            : null;
    }

    public async Task<bool> DisableAsync(string code, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/auth/mfa/disable", new { code }, ct);
        return response.IsSuccessStatusCode;
    }
}
