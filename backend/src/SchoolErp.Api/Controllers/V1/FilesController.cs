using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Application.Abstractions;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// Serves stored files. Anonymous by design: keys embed two GUIDs
/// (tenant + file), making URLs unguessable — the same model as gateway
/// checkout links — so images work in plain &lt;img&gt; tags on every client.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/files")]
public sealed class FilesController : ControllerBase
{
    private readonly IFileStorage _storage;

    public FilesController(IFileStorage storage) => _storage = storage;

    /// <summary>Streams a stored file by its full key.</summary>
    [HttpGet("{**key}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var file = await _storage.OpenAsync(key, ct);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Value.Content, file.Value.ContentType);
    }
}
