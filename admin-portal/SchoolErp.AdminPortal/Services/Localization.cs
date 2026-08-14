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

    /// <summary>
    /// The text for <paramref name="key"/> with placeholders filled in.
    /// Templates hold {0}-style slots so each language orders the sentence its
    /// own way — Telugu rarely wants the number where English puts it.
    /// </summary>
    public string Format(string key, params object?[] args) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, this[key], args);

    public async Task InitializeAsync()
    {
        var stored = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (stored is "te" or "en" && stored != Language)
        {
            Language = stored;
            // Fire even during startup: reading localStorage is async, so pages
            // routinely finish their first render BEFORE this completes — in
            // English. Without this event they stay English until the user
            // manually toggles, which read as "Telugu is broken" on every
            // direct page load with a persisted preference.
            Changed?.Invoke();
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
