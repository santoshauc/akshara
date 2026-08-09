using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Typed client for the platform billing APIs (Super Admin).</summary>
public sealed class BillingClient
{
    private readonly HttpClient _http;

    public BillingClient(HttpClient http) => _http = http;

    public async Task<TenantUsageDto?> GetUsageAsync(Guid tenantId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/billing/tenants/{tenantId}/usage", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TenantUsageDto>(cancellationToken: ct)
            : null;
    }

    public async Task<List<InvoiceDto>> GetInvoicesAsync(
        Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = tenantId is { } t ? $"?tenantId={t}" : "";
        return await _http.GetFromJsonAsync<List<InvoiceDto>>(
            $"api/v1/billing/invoices{query}", ct) ?? [];
    }

    /// <summary>Issues an invoice; error text instead of a dto on failure.</summary>
    public async Task<(InvoiceDto? Invoice, string? Error)> CreateInvoiceAsync(
        Guid tenantId, DateOnly dueOn, List<InvoiceLineDto> lines, string? notes,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/billing/invoices", new { tenantId, dueOn, lines, notes }, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<InvoiceDto>(cancellationToken: ct), null);
        }

        return (null, await ProblemResponse.ReadTitleAsync(response, ct));
    }

    /// <summary>Null on success.</summary>
    public async Task<string?> MarkPaidAsync(
        Guid invoiceId, DateOnly paidOn, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/billing/invoices/{invoiceId}/paid", new { paidOn }, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    /// <summary>Null on success.</summary>
    public async Task<string?> VoidAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"api/v1/billing/invoices/{invoiceId}/void", null, ct);
        return response.IsSuccessStatusCode ? null : await ProblemResponse.ReadTitleAsync(response, ct);
    }

    public async Task<byte[]?> GetPdfAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/billing/invoices/{invoiceId}/pdf", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(ct)
            : null;
    }

    /// <summary>The signed-in school's own subscription; null when unavailable.</summary>
    public async Task<MySubscriptionDto?> GetMySubscriptionAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/v1/subscription", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<MySubscriptionDto>(cancellationToken: ct)
            : null;
    }

    /// <summary>One of the school's own invoices as a PDF.</summary>
    public async Task<byte[]?> GetMyInvoicePdfAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/v1/subscription/invoices/{invoiceId}/pdf", ct);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadAsByteArrayAsync(ct)
            : null;
    }

    /// <summary>Sells an SMS pack: credits + invoice in one action.</summary>
    public async Task<(InvoiceDto? Invoice, string? Error)> SmsTopUpAsync(
        Guid tenantId, int credits, decimal unitPrice, DateOnly dueOn,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"api/v1/billing/tenants/{tenantId}/sms-topup",
            new { credits, unitPrice, dueOn }, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<InvoiceDto>(cancellationToken: ct), null);
        }

        return (null, await ProblemResponse.ReadTitleAsync(response, ct));
    }
}
