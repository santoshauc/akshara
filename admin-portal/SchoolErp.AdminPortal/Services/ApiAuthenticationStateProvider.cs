using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace SchoolErp.AdminPortal.Services;

/// <summary>
/// Derives the client authentication state from the stored access token by
/// decoding its payload (signature verification is the API's job — the client
/// only uses claims for UI decisions).
/// </summary>
public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly TokenStore _tokens;

    public ApiAuthenticationStateProvider(TokenStore tokens) => _tokens = tokens;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Anonymous;
        }

        var claims = ParseClaims(token);
        if (claims is null)
        {
            return Anonymous;
        }

        // Treat an expired token as signed-out; the message handler will have
        // refreshed it on the next API call anyway.
        var exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;
        if (exp is not null &&
            long.TryParse(exp, out var expSeconds) &&
            DateTimeOffset.FromUnixTimeSeconds(expSeconds) < DateTimeOffset.UtcNow)
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt",
            nameType: "unique_name", roleType: "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// The signed-in school's code, carried in the token so the chrome can
    /// brand itself. Null for a platform account.
    /// </summary>
    public async Task<string?> GetSchoolCodeAsync()
    {
        var token = await _tokens.GetAccessTokenAsync();
        return string.IsNullOrWhiteSpace(token)
            ? null
            : ParseClaims(token)?.FirstOrDefault(c => c.Type == "school_code")?.Value;
    }

    /// <summary>Call after login/logout so the UI re-renders.</summary>
    public void NotifyStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static List<Claim>? ParseClaims(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = JsonDocument.Parse(Convert.FromBase64String(payload));

            var claims = new List<Claim>();
            foreach (var property in json.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    claims.AddRange(property.Value.EnumerateArray()
                        .Select(v => new Claim(property.Name, v.ToString())));
                }
                else
                {
                    claims.Add(new Claim(property.Name, property.Value.ToString()));
                }
            }

            return claims;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
