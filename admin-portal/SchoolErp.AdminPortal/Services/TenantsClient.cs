using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Shared.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the tenant catalog APIs.</summary>
public sealed class TenantsClient
{
    private readonly HttpClient _http;

    public TenantsClient(HttpClient http) => _http = http;

    public async Task<PagedResult<TenantDto>> GetTenantsAsync(
        string? search, TenantStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }
        if (status is { } s)
        {
            query.Add($"status={s}");
        }

        return (await _http.GetFromJsonAsync<PagedResult<TenantDto>>(
            $"api/v1/tenants?{string.Join('&', query)}", ct))!;
    }

    public Task<TenantDto?> GetTenantAsync(Guid id, CancellationToken ct = default) =>
        _http.GetFromJsonAsync<TenantDto>($"api/v1/tenants/{id}", ct);

    /// <summary>Creates a school; returns the created DTO or the problem title.</summary>
    public async Task<(TenantDto? Tenant, string? Error)> CreateAsync(
        CreateTenantRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/tenants", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<TenantDto>(cancellationToken: ct), null);
        }

        return (null, await ReadProblemTitleAsync(response, ct));
    }

    /// <summary>Updates a school; returns the updated DTO or the problem title.</summary>
    public async Task<(TenantDto? Tenant, string? Error)> UpdateAsync(
        UpdateTenantRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/tenants/{request.Id}", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<TenantDto>(cancellationToken: ct), null);
        }

        return (null, await ReadProblemTitleAsync(response, ct));
    }

    public async Task<string?> ChangeStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"api/v1/tenants/{id}/status", new { status }, ct);
        return response.IsSuccessStatusCode ? null : await ReadProblemTitleAsync(response, ct);
    }

    private static async Task<string> ReadProblemTitleAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemShape>(cancellationToken: ct);
            if (problem?.Errors is { Count: > 0 } errors)
            {
                return string.Join(" ", errors.SelectMany(e => e.Value));
            }

            return problem?.Title ?? $"Request failed ({(int)response.StatusCode}).";
        }
        catch (System.Text.Json.JsonException)
        {
            return $"Request failed ({(int)response.StatusCode}).";
        }
    }

    private sealed record ProblemShape(string? Title, Dictionary<string, string[]>? Errors);

    /// <summary>Public branding by school code (anonymous); null when unknown.</summary>
    public async Task<TenantBrandingDto?> GetBrandingAsync(
        string code, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"api/v1/tenants/branding?code={Uri.EscapeDataString(code)}", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TenantBrandingDto>(cancellationToken: ct)
            : null;
    }

    /// <summary>Uploads a school logo; returns the new URL or an error.</summary>
    public async Task<(string? LogoUrl, string? Error)> UploadLogoAsync(
        Guid id, Stream file, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(file);
        content.Add(fileContent, "file", fileName);
        var response = await _http.PostAsync($"api/v1/tenants/{id}/logo", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadProblemTitleAsync(response, ct));
        }

        return (await response.Content.ReadFromJsonAsync<string>(cancellationToken: ct), null);
    }
}
