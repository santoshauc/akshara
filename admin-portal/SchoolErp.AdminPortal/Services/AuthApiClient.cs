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

    /// <summary>
    /// Password login. Success stores the session; an MFA-enabled account
    /// instead returns the challenge for <see cref="VerifyMfaAsync"/>.
    /// </summary>
    public async Task<LoginOutcome> LoginAsync(string schoolCode, string login, string password)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/auth/login", new LoginRequest(schoolCode, login, password));

        if (!response.IsSuccessStatusCode)
        {
            // 423 carries a server explanation (lockout vs expired subscription)
            // worth showing verbatim; other failures stay deliberately vague.
            if (response.StatusCode == System.Net.HttpStatusCode.Locked)
            {
                return new LoginOutcome(await ProblemResponse.ReadTitleAsync(response), null);
            }

            return new LoginOutcome("Invalid login or password.", null);
        }

        var body = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        if (body?.MfaRequired == true)
        {
            return new LoginOutcome(null, body.MfaToken);
        }

        if (body?.ChooseSchool == true)
        {
            return new LoginOutcome(null, null) { Schools = body.Schools ?? [] };
        }

        await _tokens.SetAsync(body!.AccessToken!, body.RefreshToken!);
        _authState.NotifyStateChanged();
        return new LoginOutcome(null, null);
    }

    /// <summary>
    /// Stores the signed-in school's code for the chrome's branding lookup.
    /// It comes from the token now that nobody types it; a platform sign-in
    /// has none and must not wear the last school's colours.
    /// </summary>
    public async Task RememberSchoolForBrandingAsync()
    {
        var code = await _authState.GetSchoolCodeAsync();
        if (string.IsNullOrWhiteSpace(code))
        {
            await _tokens.RemoveSchoolCodeAsync();
            return;
        }

        await _tokens.SetSchoolCodeAsync(code);
    }

    /// <summary>Second step of an MFA login. Returns null and stores the
    /// session on success; an error message otherwise.</summary>
    public async Task<string?> VerifyMfaAsync(string mfaToken, string code)
    {
        var response = await _http.PostAsJsonAsync(
            "api/v1/auth/mfa/verify", new { mfaToken, code });
        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode == System.Net.HttpStatusCode.Locked
                ? "Account is temporarily locked. Try again in 15 minutes."
                : "That code didn't match. Try the current code from your app.";
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
