using SchoolErp.Application.Abstractions;

namespace SchoolErp.Api.Services;

/// <summary>HTTP-backed <see cref="IClientContext"/> exposing the caller's remote IP.</summary>
public sealed class HttpClientContext : IClientContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpClientContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
