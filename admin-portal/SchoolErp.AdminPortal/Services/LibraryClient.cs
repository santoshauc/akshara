using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the library APIs.</summary>
public sealed class LibraryClient
{
    private readonly HttpClient _http;

    public LibraryClient(HttpClient http) => _http = http;

    public async Task<List<BookDto>> GetBooksAsync(string? search = null, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(search)
            ? ""
            : $"?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<BookDto>>($"api/v1/library/books{query}", ct) ?? [];
    }

    public async Task<string?> AddBookAsync(AddBookRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/library/books", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<List<BookLoanDto>> GetLoansAsync(
        bool overdueOnly = false, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<List<BookLoanDto>>(
            $"api/v1/library/loans?overdueOnly={overdueOnly}", ct) ?? [];

    public async Task<string?> IssueAsync(IssueBookRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/v1/library/loans", request, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<string?> ReturnAsync(Guid loanId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"api/v1/library/loans/{loanId}/return", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }
}
