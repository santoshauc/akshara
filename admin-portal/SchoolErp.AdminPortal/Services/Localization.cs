using Microsoft.JSInterop;
using SchoolErp.Shared.Localization;

namespace SchoolErp.AdminPortal.Services;

/// <summary>
/// Lightweight portal i18n, mirroring the mobile apps: a flat key → text
/// dictionary per language, English as the source of truth, Telugu required
/// to cover every key (unit-enforced). The choice persists in localStorage
/// and applies instantly — no reload, no satellite assemblies.
/// </summary>
public sealed class LocalizationService
{
    private const string StorageKey = "akshara.lang";

    private readonly IJSRuntime _js;

    public LocalizationService(IJSRuntime js) => _js = js;

    /// <summary>"en" or "te".</summary>
    public string Language { get; private set; } = "en";

    public event Action? Changed;

    /// <summary>The text for <paramref name="key"/>; the key itself when unknown.</summary>
    public string this[string key] =>
        (Language == "te" ? PortalStrings.Te : PortalStrings.En)
            .GetValueOrDefault(key)
        ?? PortalStrings.En.GetValueOrDefault(key, key);

    public async Task InitializeAsync()
    {
        var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (stored is "te" or "en")
        {
            Language = stored;
        }
    }

    public async Task SetLanguageAsync(string language)
    {
        if (language is not ("en" or "te") || language == Language)
        {
            return;
        }

        Language = language;
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, language);
        Changed?.Invoke();
    }
}
