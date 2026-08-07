using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Exams;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Exams.Queries;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Examinations: subjects, scheduling, marks entry, publication, results.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/exams")]
public sealed class ExamsController : ControllerBase
{
    private readonly ISender _sender;

    public ExamsController(ISender sender) => _sender = sender;

    /// <summary>Lists subjects.</summary>
    [HttpGet("subjects")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(typeof(IReadOnlyList<SubjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubjects(CancellationToken ct) =>
        Ok(await _sender.Send(new GetSubjectsQuery(), ct));

    /// <summary>Creates a subject.</summary>
    [HttpPost("subjects")]
    [HasPermission(Permissions.Examinations.Manage)]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSubject(
        [FromBody] CreateSubjectCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return Created($"api/v1/exams/subjects/{result.Id}", result);
    }

    /// <summary>Lists exams of an academic year with their papers.</summary>
    [HttpGet]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(typeof(IReadOnlyList<ExamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExams([FromQuery] Guid academicYearId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetExamsQuery(academicYearId), ct));

    /// <summary>Creates an exam in Draft.</summary>
    [HttpPost]
    [HasPermission(Permissions.Examinations.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateExam(
        [FromBody] CreateExamCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return Created($"api/v1/exams/{id}", id);
    }

    /// <summary>Schedules a paper (subject for a class) inside an exam.</summary>
    [HttpPost("{examId:guid}/subjects")]
    [HasPermission(Permissions.Examinations.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScheduleSubject(
        Guid examId, [FromBody] SchedulePaperRequest request, CancellationToken ct)
    {
        var id = await _sender.Send(new ScheduleExamSubjectCommand(
            examId, request.SchoolClassId, request.SubjectId,
            request.ExamDate, request.MaxMarks, request.PassMarks), ct);
        return Created($"api/v1/exams/{examId}/subjects/{id}", id);
    }

    /// <summary>The marks-entry grid for a paper.</summary>
    [HttpGet("papers/{examSubjectId:guid}/marks")]
    [HasPermission(Permissions.Examinations.EnterMarks)]
    [ProducesResponseType(typeof(MarksGridDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarksGrid(Guid examSubjectId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetMarksGridQuery(examSubjectId), ct));

    /// <summary>Enters or corrects marks for a paper.</summary>
    [HttpPost("papers/{examSubjectId:guid}/marks")]
    [HasPermission(Permissions.Examinations.EnterMarks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnterMarks(
        Guid examSubjectId, [FromBody] EnterMarksRequest request, CancellationToken ct)
    {
        await _sender.Send(new EnterMarksCommand(examSubjectId, request.Entries), ct);
        return NoContent();
    }

    /// <summary>Publishes the exam: freezes marks and notifies parents.</summary>
    [HttpPost("{examId:guid}/publish")]
    [HasPermission(Permissions.Examinations.PublishResults)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid examId, CancellationToken ct)
    {
        await _sender.Send(new PublishExamCommand(examId), ct);
        return NoContent();
    }

    /// <summary>A student's computed result for an exam.</summary>
    [HttpGet("{examId:guid}/results/{studentId:guid}")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(typeof(StudentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentResult(
        Guid examId, Guid studentId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentResultQuery(studentId, examId), ct));
}

/// <summary>Paper-scheduling payload.</summary>
public sealed record SchedulePaperRequest(
    Guid SchoolClassId, Guid SubjectId, DateOnly? ExamDate, decimal MaxMarks, decimal PassMarks);

/// <summary>Marks-entry payload.</summary>
public sealed record EnterMarksRequest(IReadOnlyList<MarkInput> Entries);
