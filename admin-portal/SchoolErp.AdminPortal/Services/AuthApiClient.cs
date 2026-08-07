using System.Net.Http.Json;
using SchoolErp.AdminPortal.Models;

namespace SchoolErp.AdminPortal.Services;

/// <summary>
/// Talks to the auth endpoints on a bare HttpClient (no auth handler — these
/// calls must never recurse into the refresh logic).
/// </summary>
public sealed class AuthApiClient
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;
    private readonly ApiAuthenticationStateProvider _authState;

    public AuthApiClient(HttpClient http, TokenStore tokens, ApiAuthenticationStateProvider authState)
    {
        _http = http;
        _tokens = tokens;
        _authState = authState;
    }

    /// <summary>Password login. Returns null on invalid credentials/lockout.</summary>
    public async Task<string?> LoginAsync(string schoolCode, string login, string password)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/auth/login", new LoginRequest(schoolCode, login, password));

        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode == System.Net.HttpStatusCode.Locked
                ? "Account is temporarily locked. Try again in 15 minutes."
                : "Invalid school code, login or password.";
        }

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensDto>();
        await _tokens.SetAsync(tokens!.AccessToken, tokens.RefreshToken);
        _authState.NotifyStateChanged();
        return null;
    }

    /// <summary>Rotates the refresh token. Returns false when the session is dead.</summary>
    public async Task<bool> TryRefreshAsync()
    {
        var refreshToken = await _tokens.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var response = await _http.PostAsJsonAsync("api/v1/auth/refresh", new { refreshToken });
        if (!response.IsSuccessStatusCode)
        {
            await _tokens.ClearAsync();
            _authState.NotifyStateChanged();
            return false;
        }

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensDto>();
        await _tokens.SetAsync(tokens!.AccessToken, tokens.RefreshToken);
        return true;
    }

    /// <summary>Revokes the refresh token and clears local state.</summary>
    public async Task LogoutAsync()
    {
        var refreshToken = await _tokens.GetRefreshTokenAsync();
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await _http.PostAsJsonAsync("api/v1/auth/logout", new { refreshToken });
            }
            catch (HttpRequestException)
            {
                // Offline logout still clears the local session.
            }
        }

        await _tokens.ClearAsync();
        _authState.NotifyStateChanged();
    }
}
