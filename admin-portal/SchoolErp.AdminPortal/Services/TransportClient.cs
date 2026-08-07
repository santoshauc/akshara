using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the transport APIs.</summary>
public sealed class TransportClient
{
    private readonly HttpClient _http;

    public TransportClient(HttpClient http) => _http = http;

    public async Task<List<VehicleDto>> GetVehiclesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<VehicleDto>>("api/v1/transport/vehicles", ct) ?? [];

    public async Task<string?> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/transport/vehicles", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<TransportRouteDto>> GetRoutesAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<TransportRouteDto>>("api/v1/transport/routes", ct) ?? [];

    public async Task<string?> CreateRouteAsync(CreateRouteRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/transport/routes", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> AssignStudentAsync(AssignTransportRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("api/v1/transport/assignments", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
