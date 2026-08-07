using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Application.Attendance.Queries;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Communication;
using SchoolErp.Application.Exams;
using SchoolErp.Application.Exams.Commands;
using SchoolErp.Application.Exams.Queries;
using SchoolErp.Application.Fees;
using SchoolErp.Application.Fees.Queries;
using SchoolErp.Application.Homework;
using SchoolErp.Application.Parent;
using SchoolErp.Application.Transport;
using SchoolErp.Domain.Exams;
using SchoolErp.Infrastructure.Identity;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>
/// The parent app's API. No permission claims required — just an authenticated
/// tenant user; every child-scoped call goes through <see cref="ParentAccess"/>
/// so a parent can only ever read their own children. Unpublished exam results
/// are invisible here regardless of staff-side state.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/parent")]
[Authorize]
public sealed class ParentController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ParentAccess _access;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _db;

    public ParentController(
        ISender sender,
        ParentAccess access,
        ICurrentUser currentUser,
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext db)
    {
        _sender = sender;
        _access = access;
        _currentUser = currentUser;
        _userManager = userManager;
        _db = db;
    }

    /// <summary>The signed-in parent's children.</summary>
    [HttpGet("children")]
    [ProducesResponseType(typeof(IReadOnlyList<ChildDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildren(CancellationToken ct)
    {
        var phone = await GetUserPhoneAsync();
        return Ok(await _sender.Send(new GetMyChildrenQuery(_currentUser.UserId, phone), ct));
    }

    /// <summary>A child's attendance calendar for a month.</summary>
    [HttpGet("children/{studentId:guid}/attendance")]
    [ProducesResponseType(typeof(StudentMonthAttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildAttendance(
        Guid studentId, [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        return Ok(await _sender.Send(new GetStudentMonthAttendanceQuery(studentId, year, month), ct));
    }

    /// <summary>Published exams of the current academic year.</summary>
    [HttpGet("children/{studentId:guid}/exams")]
    [ProducesResponseType(typeof(IReadOnlyList<ExamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildExams(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        var yearId = await GetCurrentYearIdAsync(ct);
        if (yearId is null)
        {
            return Ok(Array.Empty<ExamDto>());
        }

        var exams = await _sender.Send(new GetExamsQuery(yearId.Value), ct);
        return Ok(exams.Where(e => e.Status == ExamStatus.Published).ToList());
    }

    /// <summary>A child's result for a published exam.</summary>
    [HttpGet("children/{studentId:guid}/exams/{examId:guid}/result")]
    [ProducesResponseType(typeof(StudentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildResult(
        Guid studentId, Guid examId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        var result = await _sender.Send(new GetStudentResultQuery(studentId, examId), ct);
        if (result.ExamStatus != ExamStatus.Published)
        {
            // Draft results are staff-only; to a parent they do not exist.
            throw new NotFoundException("Result", examId);
        }

        return Ok(result);
    }

    /// <summary>A child's fee ledger for the current academic year.</summary>
    [HttpGet("children/{studentId:guid}/fees")]
    [ProducesResponseType(typeof(StudentFeeSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildFees(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        var yearId = await GetCurrentYearIdAsync(ct)
            ?? throw new NotFoundException("Current academic year", studentId);
        return Ok(await _sender.Send(new GetStudentFeeSummaryQuery(studentId, yearId), ct));
    }

    /// <summary>Notices visible to a child (school-wide + their class, unexpired).</summary>
    [HttpGet("children/{studentId:guid}/notices")]
    [ProducesResponseType(typeof(IReadOnlyList<NoticeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildNotices(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        return Ok(await _sender.Send(
            new GetStudentNoticesQuery(studentId, DateOnly.FromDateTime(DateTime.UtcNow)), ct));
    }

    /// <summary>Homework for a child's class/section.</summary>
    [HttpGet("children/{studentId:guid}/homework")]
    [ProducesResponseType(typeof(IReadOnlyList<HomeworkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildHomework(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        return Ok(await _sender.Send(new GetStudentHomeworkQuery(studentId), ct));
    }

    /// <summary>Live bus location for a child's route (204 when no active trip).</summary>
    [HttpGet("children/{studentId:guid}/bus")]
    [ProducesResponseType(typeof(BusLocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetChildBusLocation(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        var location = await _sender.Send(new GetBusLocationQuery(studentId), ct);
        return location is null ? NoContent() : Ok(location);
    }

    /// <summary>A child's transport allocation (204 when none).</summary>
    [HttpGet("children/{studentId:guid}/transport")]
    [ProducesResponseType(typeof(ChildTransportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetChildTransport(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        var transport = await _sender.Send(new GetChildTransportQuery(studentId), ct);
        return transport is null ? NoContent() : Ok(transport);
    }

    private async Task EnsureChildAsync(Guid studentId, CancellationToken ct) =>
        await _access.EnsureChildAsync(_currentUser.UserId, await GetUserPhoneAsync(), studentId, ct);

    private async Task<string?> GetUserPhoneAsync()
    {
        if (_currentUser.UserId is not { } userId)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId);
        return user?.PhoneNumber;
    }

    private Task<Guid?> GetCurrentYearIdAsync(CancellationToken ct) =>
        _db.AcademicYears.Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(ct);
}
