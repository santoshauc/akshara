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
using SchoolErp.Application.Fees.Commands;
using SchoolErp.Application.Fees.Queries;
using SchoolErp.Application.Homework;
using SchoolErp.Application.Hostel;
using SchoolErp.Application.Leave;
using SchoolErp.Application.Library;
using SchoolErp.Application.Parent;
using SchoolErp.Application.Timetable;
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

    /// <summary>A child's published weekly timetable.</summary>
    [HttpGet("children/{studentId:guid}/timetable")]
    [ProducesResponseType(typeof(IReadOnlyList<TimetableEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildTimetable(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        return Ok(await _sender.Send(new GetStudentTimetableQuery(studentId), ct));
    }

    /// <summary>A child's library loans (history; open loans first by recency).</summary>
    [HttpGet("children/{studentId:guid}/library")]
    [ProducesResponseType(typeof(IReadOnlyList<BookLoanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildLibrary(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        return Ok(await _sender.Send(new GetLoansQuery(studentId), ct));
    }

    /// <summary>A child's hostel stay (204 for day scholars).</summary>
    [HttpGet("children/{studentId:guid}/hostel")]
    [ProducesResponseType(typeof(ChildHostelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetChildHostel(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        var stay = await _sender.Send(new GetStudentHostelQuery(studentId), ct);
        return stay is null ? NoContent() : Ok(stay);
    }

    /// <summary>
    /// Creates an online payment order for the child's fees and returns the
    /// checkout URL to open in a browser. The webhook completes the payment.
    /// </summary>
    [HttpPost("children/{studentId:guid}/fees/orders")]
    [ProducesResponseType(typeof(ParentPaymentOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFeeOrder(
        Guid studentId, [FromBody] ParentFeeOrderRequest request, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);

        var yearId = await _db.AcademicYears.AsNoTracking()
            .Where(y => y.IsCurrent)
            .Select(y => (Guid?)y.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Current academic year", "none");

        var order = await _sender.Send(
            new CreatePaymentOrderCommand(studentId, yearId, request.Amount), ct);
        var checkoutUrl =
            $"{Request.Scheme}://{Request.Host}/api/v1/payments/checkout/{order.GatewayOrderId}";
        return Ok(new ParentPaymentOrderResponse(
            order.OrderId, order.GatewayOrderId, order.Amount, checkoutUrl));
    }

    /// <summary>A child's report card for a published exam, as a PDF.</summary>
    [HttpGet("children/{studentId:guid}/exams/{examId:guid}/report-card")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChildReportCard(
        Guid studentId, Guid examId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        var pdf = await _sender.Send(
            new GetReportCardPdfQuery(studentId, examId, PublishedOnly: true), ct);
        return File(pdf, "application/pdf", "report-card.pdf");
    }

    /// <summary>A child's leave requests, newest first.</summary>
    [HttpGet("children/{studentId:guid}/leave-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildLeaveRequests(Guid studentId, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        return Ok(await _sender.Send(new GetStudentLeaveRequestsQuery(studentId), ct));
    }

    /// <summary>Submits a leave request for a child (pending staff approval).</summary>
    [HttpPost("children/{studentId:guid}/leave-requests")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitChildLeaveRequest(
        Guid studentId, [FromBody] ParentLeaveRequest request, CancellationToken ct)
    {
        await EnsureChildAsync(studentId, ct);
        return Ok(await _sender.Send(new SubmitLeaveRequestCommand(
            studentId, request.FromDate, request.ToDate, request.Reason), ct));
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

/// <summary>Parent online-payment order payload.</summary>
public sealed record ParentFeeOrderRequest(
    [System.ComponentModel.DataAnnotations.Range(1, 10_00_000)] decimal Amount);

/// <summary>Order + checkout URL as returned to the parent app.</summary>
public sealed record ParentPaymentOrderResponse(
    Guid OrderId, string GatewayOrderId, decimal Amount, string CheckoutUrl);

/// <summary>Parent leave-request payload.</summary>
public sealed record ParentLeaveRequest(DateOnly FromDate, DateOnly ToDate, string Reason);
