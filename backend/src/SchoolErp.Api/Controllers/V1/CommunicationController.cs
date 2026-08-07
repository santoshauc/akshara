using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Communication;
using SchoolErp.Application.Homework;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Notices/circulars.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notices")]
public sealed class NoticesController : ControllerBase
{
    private readonly ISender _sender;

    public NoticesController(ISender sender) => _sender = sender;

    /// <summary>Latest notices (staff view).</summary>
    [HttpGet]
    [HasPermission(Permissions.Communication.View)]
    [ProducesResponseType(typeof(IReadOnlyList<NoticeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotices([FromQuery] int limit = 50, CancellationToken ct = default) =>
        Ok(await _sender.Send(new GetNoticesQuery(limit), ct));

    /// <summary>Publishes a notice.</summary>
    [HttpPost]
    [HasPermission(Permissions.Communication.Send)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateNotice(
        [FromBody] CreateNoticeCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return Created($"api/v1/notices/{id}", id);
    }
}

/// <summary>Homework assignments.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/homework")]
public sealed class HomeworkController : ControllerBase
{
    private readonly ISender _sender;

    public HomeworkController(ISender sender) => _sender = sender;

    /// <summary>Recent homework of a class (staff view).</summary>
    [HttpGet]
    [HasPermission(Permissions.Homework.View)]
    [ProducesResponseType(typeof(IReadOnlyList<HomeworkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClassHomework(
        [FromQuery] Guid classId, [FromQuery] int limit = 50, CancellationToken ct = default) =>
        Ok(await _sender.Send(new GetClassHomeworkQuery(classId, limit), ct));

    /// <summary>Posts homework.</summary>
    [HttpPost]
    [HasPermission(Permissions.Homework.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateHomework(
        [FromBody] CreateHomeworkCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return Created($"api/v1/homework/{id}", id);
    }
}
