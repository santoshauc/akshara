using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Application.Exams;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Exams.Queries;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Examinations: subjects, scheduling, marks entry, publication, results.</summary>
[ApiController]
[RequiresModule(TenantModules.Examination)]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/exams")]
public sealed class ExamsController : ControllerBase
{
    private readonly ISender _sender;

    public ExamsController(ISender sender) => _sender = sender;

    /// <summary>
    /// A student's cumulative grade sheet: every published semester with its
    /// SGPA, and the CGPA across them. Colleges on a credit system; a school's
    /// papers carry no credits, so it reports as unavailable rather than zero.
    /// </summary>
    [HttpGet("students/{studentId:guid}/grade-sheet")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(typeof(GradeSheetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGradeSheet(Guid studentId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentGradeSheetQuery(studentId), ct));

    /// <summary>
    /// The grading ordinance in force, and whether it is the institution's own
    /// or the UGC fallback.
    /// </summary>
    [HttpGet("grade-scale")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(typeof(GradeScaleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGradeScale(CancellationToken ct) =>
        Ok(await _sender.Send(new GetGradeScaleQuery(), ct));

    /// <summary>
    /// Replaces the ordinance. Already-published results keep the grades they
    /// were computed with; a transcript that changes after the fact is worse
    /// than one that is out of date.
    /// </summary>
    [HttpPut("grade-scale")]
    [HasPermission(Permissions.Examinations.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetGradeScale(
        [FromBody] SetGradeScaleCommand command, CancellationToken ct)
    {
        await _sender.Send(command, ct);
        return NoContent();
    }

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

    /// <summary>The student's report card for an exam as a PDF (drafts allowed for proofing).</summary>
    [HttpGet("{examId:guid}/results/{studentId:guid}/report-card")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportCard(Guid examId, Guid studentId, CancellationToken ct)
    {
        var pdf = await _sender.Send(new GetReportCardPdfQuery(studentId, examId), ct);
        return File(pdf, "application/pdf", $"report-card-{studentId:N}.pdf");
    }

    /// <summary>Term report definitions of a year.</summary>
    [HttpGet("term-reports")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(typeof(IReadOnlyList<TermReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTermReports(
        [FromQuery] Guid academicYearId, CancellationToken ct) =>
        Ok(await _sender.Send(new GetTermReportsQuery(academicYearId), ct));

    /// <summary>Creates a term report from weighted exams (weights sum to 100).</summary>
    [HttpPost("term-reports")]
    [HasPermission(Permissions.Examinations.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTermReport(
        [FromBody] CreateTermReportRequest request, CancellationToken ct) =>
        Ok(await _sender.Send(new CreateTermReportCommand(
            request.AcademicYearId,
            request.Name,
            request.Components.Select(c => (c.ExamId, c.WeightPercent)).ToList()), ct));

    /// <summary>Upserts a student's co-scholastic grades and remarks.</summary>
    [HttpPut("term-reports/{id:guid}/students/{studentId:guid}")]
    [HasPermission(Permissions.Examinations.EnterMarks)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTermStudentInput(
        Guid id, Guid studentId, [FromBody] TermStudentInputRequest request, CancellationToken ct)
    {
        await _sender.Send(new SetTermStudentInputCommand(
            id, studentId, request.CoScholastic, request.Remarks), ct);
        return NoContent();
    }

    /// <summary>The weighted term report card as a PDF (drafts allowed for staff).</summary>
    [HttpGet("term-reports/{id:guid}/students/{studentId:guid}/pdf")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTermReportCard(
        Guid id, Guid studentId, CancellationToken ct)
    {
        var pdf = await _sender.Send(new GetTermReportCardPdfQuery(id, studentId), ct);
        return File(pdf, "application/pdf", "term-report.pdf");
    }

    /// <summary>How this school's report cards are laid out.</summary>
    [HttpGet("report-card-settings")]
    [HasPermission(Permissions.Examinations.View)]
    [ProducesResponseType(typeof(ReportCardSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportCardSettings(CancellationToken ct) =>
        Ok(await _sender.Send(new GetReportCardSettingsQuery(), ct));

    /// <summary>Changes the report-card layout for this school.</summary>
    [HttpPut("report-card-settings")]
    [HasPermission(Permissions.Examinations.Manage)]
    [ProducesResponseType(typeof(ReportCardSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateReportCardSettings(
        [FromBody] UpdateReportCardSettingsCommand command, CancellationToken ct) =>
        Ok(await _sender.Send(command, ct));
}

/// <summary>Term report creation payload.</summary>
public sealed record CreateTermReportRequest(
    Guid AcademicYearId,
    string Name,
    IReadOnlyList<TermReportComponentInput> Components);

/// <summary>One weighted exam in the creation payload.</summary>
public sealed record TermReportComponentInput(Guid ExamId, decimal WeightPercent);

/// <summary>Co-scholastic + remarks payload.</summary>
public sealed record TermStudentInputRequest(
    Dictionary<string, string> CoScholastic, string? Remarks);

/// <summary>Paper-scheduling payload.</summary>
public sealed record SchedulePaperRequest(
    Guid SchoolClassId, Guid SubjectId, DateOnly? ExamDate, decimal MaxMarks, decimal PassMarks);

/// <summary>Marks-entry payload.</summary>
public sealed record EnterMarksRequest(IReadOnlyList<MarkInput> Entries);
