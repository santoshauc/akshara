using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for staff-account and role administration.</summary>
public sealed class UserAdminClient
{
    private readonly HttpClient _http;

    public UserAdminClient(HttpClient http) => _http = http;

    public async Task<List<StaffUserDto>> GetUsersAsync(
        string? search = null, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(search)
            ? ""
            : $"?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<StaffUserDto>>($"api/v1/users{query}", ct) ?? [];
    }

    public async Task<string?> CreateUserAsync(
        CreateStaffUserRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/users", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> UpdateUserAsync(
        UpdateStaffUserRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/users/{request.UserId}", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> ResetPasswordAsync(
        Guid userId, string newPassword, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/users/{userId}/reset-password", new { userId, newPassword }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<RoleDto>>("api/v1/users/roles", ct) ?? [];

    public async Task<List<string>> GetPermissionCatalogAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<string>>("api/v1/users/permissions", ct) ?? [];

    public async Task<string?> CreateRoleAsync(
        CreateRoleRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/users/roles", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> UpdateRoleAsync(
        UpdateRoleRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/v1/users/roles/{request.RoleId}", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Self-service password change. Null on success.</summary>
    public async Task<string?> ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/auth/password/change", new { currentPassword, newPassword }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
