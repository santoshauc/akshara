using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Application.Students.Queries;
using SchoolErp.Domain.Students;
using SchoolErp.Shared.Authorization;
using SchoolErp.Shared.Models;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Student information system: admissions and student records.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/students")]
public sealed class StudentsController : ControllerBase
{
    private readonly ISender _sender;

    public StudentsController(ISender sender) => _sender = sender;

    /// <summary>Paged student list with search and placement filters.</summary>
    [HttpGet]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(typeof(PagedResult<StudentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? search,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? classId,
        [FromQuery] Guid? sectionId,
        [FromQuery] StudentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetStudentsQuery(search, academicYearId, classId, sectionId, status, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Full student detail with guardians and current placement.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(typeof(StudentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudent(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentByIdQuery(id), ct));

    /// <summary>Admits a student with guardians and initial enrollment.</summary>
    [HttpPost]
    [HasPermission(Permissions.Students.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdmitStudent(
        [FromBody] AdmitStudentCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetStudent), new { id, version = "1" }, id);
    }
}
