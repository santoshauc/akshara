using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Communication;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Staff side of parent↔school messaging.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/messages")]
public sealed class MessagesController : ControllerBase
{
    private readonly ISender _sender;

    public MessagesController(ISender sender) => _sender = sender;

    /// <summary>Every conversation, unread-first.</summary>
    [HttpGet("threads")]
    [HasPermission(Permissions.Communication.View)]
    [ProducesResponseType(typeof(IReadOnlyList<MessageThreadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetThreads(CancellationToken ct) =>
        Ok(await _sender.Send(new GetMessageThreadsQuery(), ct));

    /// <summary>A student's conversation; opening it marks parent messages read.</summary>
    [HttpGet("students/{studentId:guid}")]
    [HasPermission(Permissions.Communication.View)]
    [ProducesResponseType(typeof(IReadOnlyList<StudentMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversation(Guid studentId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentMessagesQuery(studentId, AsStaff: true), ct));

    /// <summary>Sends a staff reply on the student's thread.</summary>
    [HttpPost("students/{studentId:guid}")]
    [HasPermission(Permissions.Communication.Send)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(
        Guid studentId, [FromBody] SendMessageRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new SendStudentMessageCommand(
            studentId, request.Body, SentByStaff: true), ct));
}

/// <summary>Message body payload.</summary>
public sealed record SendMessageRequest([Required][StringLength(2048)] string Body);
