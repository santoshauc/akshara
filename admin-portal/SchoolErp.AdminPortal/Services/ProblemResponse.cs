using System.Net.Http.Json;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Extracts a human-readable message from an RFC 7807 problem response.</summary>
public static class ProblemResponse
{
    public static async Task<string> ReadTitleAsync(
        HttpResponseMessage response, CancellationToken ct = default)
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
}
