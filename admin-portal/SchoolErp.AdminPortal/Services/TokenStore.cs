using Blazored.LocalStorage;

namespace SchoolErp.AdminPortal.Services;

/// <summary>Browser-local persistence for the token pair.</summary>
public sealed class TokenStore
{
    private const string AccessKey = "schoolerp.access";
    private const string RefreshKey = "schoolerp.refresh";

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
}
