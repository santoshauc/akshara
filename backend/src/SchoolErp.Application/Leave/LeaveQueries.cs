using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Leave;

namespace SchoolErp.Application.Leave;

/// <summary>Staff inbox: all requests, optionally filtered by status.</summary>
public sealed record GetLeaveRequestsQuery(LeaveRequestStatus? Status = null)
    : IRequest<IReadOnlyList<LeaveRequestDto>>;

/// <summary>Newest first; names resolved from student/teacher records.</summary>
public sealed class GetLeaveRequestsQueryHandler
    : IRequestHandler<GetLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    private readonly IApplicationDbContext _db;

    public GetLeaveRequestsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(
        GetLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.LeaveRequests.AsNoTracking();
        if (request.Status is { } status)
        {
            query = query.Where(l => l.Status == status);
        }

        return await ProjectAsync(_db, query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Shared projection so every list shape stays identical.</summary>
    internal static async Task<IReadOnlyList<LeaveRequestDto>> ProjectAsync(
        IApplicationDbContext db, IQueryable<LeaveRequest> query, CancellationToken ct) =>
        await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .Select(l => new LeaveRequestDto(
                l.Id,
                l.Kind,
                l.StudentId,
                l.StudentId != null
                    ? db.Students.Where(s => s.Id == l.StudentId)
                        .Select(s => (s.FirstName + " " + s.LastName).Trim()).First()
                    : l.TeacherId != null
                        ? db.Teachers.Where(t => t.Id == l.TeacherId)
                            .Select(t => t.FullName).First()
                        : "Staff member",
                l.StudentId != null
                    ? db.Enrollments
                        .Where(e => e.StudentId == l.StudentId &&
                                    e.Status == Domain.Students.EnrollmentStatus.Active)
                        .Select(e => e.SchoolClass!.Name + " " + e.Section!.Name)
                        .FirstOrDefault()
                    : null,
                l.FromDate,
                l.ToDate,
                l.Reason,
                l.Status,
                l.DecisionNote,
                l.CreatedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);
}

/// <summary>One student's leave history (parent app card).</summary>
public sealed record GetStudentLeaveRequestsQuery(Guid StudentId)
    : IRequest<IReadOnlyList<LeaveRequestDto>>;

/// <summary>Delegates to the shared projection.</summary>
public sealed class GetStudentLeaveRequestsQueryHandler
    : IRequestHandler<GetStudentLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    private readonly IApplicationDbContext _db;

    public GetStudentLeaveRequestsQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<IReadOnlyList<LeaveRequestDto>> Handle(
        GetStudentLeaveRequestsQuery request, CancellationToken cancellationToken) =>
        GetLeaveRequestsQueryHandler.ProjectAsync(
            _db,
            _db.LeaveRequests.AsNoTracking().Where(l => l.StudentId == request.StudentId),
            cancellationToken);
}

/// <summary>The signed-in staff member's own leave requests.</summary>
public sealed record GetMyLeaveRequestsQuery : IRequest<IReadOnlyList<LeaveRequestDto>>;

/// <summary>Filters by the caller's user id.</summary>
public sealed class GetMyLeaveRequestsQueryHandler
    : IRequestHandler<GetMyLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetMyLeaveRequestsQueryHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<LeaveRequestDto>> Handle(
        GetMyLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
        {
            return Task.FromResult<IReadOnlyList<LeaveRequestDto>>([]);
        }

        return GetLeaveRequestsQueryHandler.ProjectAsync(
            _db,
            _db.LeaveRequests.AsNoTracking()
                .Where(l => l.RequestedByUserId == userId && l.StudentId == null),
            cancellationToken);
    }
}
