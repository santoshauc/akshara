using System.Net;
using System.Net.Http.Headers;

namespace SchoolErp.AdminPortal.Services;

/// <summary>
/// Attaches the bearer token to every API call and transparently refreshes the
/// session once on a 401 before failing.
/// </summary>
public sealed class AuthMessageHandler : DelegatingHandler
{
    private readonly TokenStore _tokens;
    private readonly AuthApiClient _authApi;

    public AuthMessageHandler(TokenStore tokens, AuthApiClient authApi)
    {
        _tokens = tokens;
        _authApi = authApi;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachTokenAsync(request);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!await _authApi.TryRefreshAsync())
        {
            return response; // session dead — UI redirects to login
        }

        response.Dispose();
        await AttachTokenAsync(request);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
