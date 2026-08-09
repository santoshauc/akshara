using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the school store.</summary>
public sealed class InventoryClient
{
    private readonly HttpClient _http;

    public InventoryClient(HttpClient http) => _http = http;

    public async Task<List<InventoryItemDto>> GetItemsAsync(
        string? search = null, bool lowOnly = false, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (lowOnly)
        {
            query.Add("lowOnly=true");
        }

        var suffix = query.Count == 0 ? "" : "?" + string.Join('&', query);
        return await _http.GetFromJsonAsync<List<InventoryItemDto>>(
            $"api/v1/inventory/items{suffix}", ct) ?? [];
    }

    public async Task<string?> CreateItemAsync(
        CreateInventoryItemRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/inventory/items", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(
        Guid? itemId = null, CancellationToken ct = default)
    {
        var suffix = itemId is { } id ? $"?itemId={id}" : "";
        return await _http.GetFromJsonAsync<List<StockMovementDto>>(
            $"api/v1/inventory/movements{suffix}", ct) ?? [];
    }

    public async Task<string?> RecordMovementAsync(
        RecordStockMovementRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/inventory/movements", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
