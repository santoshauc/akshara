using Blazored.LocalStorage;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Browser-local persistence for the token pair.</summary>
public sealed class TokenStore
{
    private const string AccessKey = "schoolerp.access";
    private const string RefreshKey = "schoolerp.refresh";
    private const string SchoolCodeKey = "akshara.schoolCode";

    private readonly ILocalStorageService _storage;

    public TokenStore(ILocalStorageService storage) => _storage = storage;

    public ValueTask<string?> GetAccessTokenAsync() => _storage.GetItemAsync<string?>(AccessKey);

    public ValueTask<string?> GetRefreshTokenAsync() => _storage.GetItemAsync<string?>(RefreshKey);

    public async ValueTask SetAsync(string accessToken, string refreshToken)
    {
        await _storage.SetItemAsync(AccessKey, accessToken);
        await _storage.SetItemAsync(RefreshKey, refreshToken);
    }

    public async ValueTask ClearAsync()
    {
        await _storage.RemoveItemAsync(AccessKey);
        await _storage.RemoveItemAsync(RefreshKey);
    }

    /// <summary>
    /// The school whose branding the chrome should wear. Kept separate from the
    /// token so the layout can read it synchronously on first paint, before any
    /// API call.
    /// <para>
    /// Stored AS A STRING, not JSON: MainLayout reads this with a raw
    /// <c>localStorage.getItem</c>, and <c>SetItemAsync</c> would wrap it in
    /// quotes that the branding lookup then fails to match.
    /// </para>
    /// </summary>
    public ValueTask SetSchoolCodeAsync(string code) =>
        _storage.SetItemAsStringAsync(SchoolCodeKey, code.Trim().ToUpperInvariant());

    public ValueTask RemoveSchoolCodeAsync() => _storage.RemoveItemAsync(SchoolCodeKey);
}
