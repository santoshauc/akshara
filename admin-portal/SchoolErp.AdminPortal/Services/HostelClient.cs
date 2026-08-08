using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the hostel APIs.</summary>
public sealed class HostelClient
{
    private readonly HttpClient _http;

    public HostelClient(HttpClient http) => _http = http;

    public async Task<List<HostelDto>> GetHostelsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<HostelDto>>("api/v1/hostel", ct) ?? [];

    public async Task<string?> CreateHostelAsync(
        CreateHostelRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/hostel", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<HostelRoomDto>> GetRoomsAsync(
        Guid hostelId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<HostelRoomDto>>(
            $"api/v1/hostel/{hostelId}/rooms", ct) ?? [];

    public async Task<string?> AddRoomAsync(
        Guid hostelId, AddHostelRoomRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"api/v1/hostel/{hostelId}/rooms", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<HostelAllocationDto>> GetAllocationsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<HostelAllocationDto>>(
            "api/v1/hostel/allocations", ct) ?? [];

    public async Task<string?> AllocateAsync(AllocateRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/hostel/allocations", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> VacateAsync(Guid allocationId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"api/v1/hostel/allocations/{allocationId}/vacate", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
