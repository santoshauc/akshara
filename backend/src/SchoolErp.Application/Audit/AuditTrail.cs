using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Audit;

namespace SchoolErp.Application.Audit;

/// <summary>
/// Appends one <see cref="AuditEvent"/> row for every successfully handled
/// command (request type ending in "Command"). Queries are never logged.
/// The row is written AFTER the handler's own SaveChanges — if the command
/// fails, nothing is logged; if the audit write fails, the command has
/// already succeeded and the error surfaces normally (audit is mandatory,
/// not best-effort, for DPDP accountability).
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IClientContext _clientContext;

    public AuditBehavior(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        IClientContext clientContext)
    {
        _db = db;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _clientContext = clientContext;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        if (typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal))
        {
            _db.AuditEvents.Add(new AuditEvent
            {
                TenantId = _tenantContext.HasTenant ? _tenantContext.TenantId : null,
                UserId = _currentUser.UserId,
                UserName = _currentUser.UserName,
                Action = typeof(TRequest).Name,
                IpAddress = _clientContext.IpAddress,
                OccurredAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}

/// <summary>
/// One row of the audit trail as shown to staff. <paramref name="SchoolName"/>
/// is only populated for the platform view, where rows can come from different
/// schools — a school's own trail is already all one school.
/// </summary>
public sealed record AuditEventDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string? UserName,
    string? UserId,
    string? IpAddress,
    string? SchoolName = null);

/// <summary>
/// The action trail. A school admin always gets their own school's rows and
/// <paramref name="SchoolId"/> is ignored — the tenant decides.
/// <para>
/// A platform caller gets OPERATOR actions (the tenant-less rows: plan edits,
/// SMS top-ups, invoices) by default, or one named school's trail when
/// <paramref name="SchoolId"/> is given. It deliberately no longer returns
/// every school at once: that firehose mixed thousands of unrelated school
/// rows into the view meant for holding operators to account.
/// </para>
/// Latest first, capped at 200.
/// </summary>
public sealed record GetAuditTrailQuery(
    string? Search = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? SchoolId = null) : IRequest<IReadOnlyList<AuditEventDto>>;

/// <summary>Explicit tenant filter — this table has no RLS (nullable tenant).</summary>
public sealed class GetAuditTrailQueryHandler
    : IRequestHandler<GetAuditTrailQuery, IReadOnlyList<AuditEventDto>>
{
    private const int MaxRows = 200;

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetAuditTrailQueryHandler(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<AuditEventDto>> Handle(
        GetAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var query = _db.AuditEvents.AsNoTracking();

        if (_tenantContext.HasTenant)
        {
            // A school never sees outside itself, whatever it asks for.
            var tenantId = _tenantContext.TenantId;
            query = query.Where(a => a.TenantId == tenantId);
        }
        else if (request.SchoolId is { } schoolId)
        {
            query = query.Where(a => a.TenantId == schoolId);
        }
        else
        {
            // Operator actions only. Tenant-less AND performed by a signed-in
            // person: anonymous public traffic (the website enquiry form) also
            // lands without a tenant, and it is not something an operator did.
            query = query.Where(a => a.TenantId == null && a.UserId != null);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a =>
                EF.Functions.ILike(a.Action, $"%{term}%") ||
                (a.UserName != null && EF.Functions.ILike(a.UserName, $"%{term}%")));
        }

        if (request.From is { } from)
        {
            var fromInstant = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(a => a.OccurredAt >= fromInstant);
        }

        if (request.To is { } to)
        {
            var toInstant = new DateTimeOffset(
                to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(a => a.OccurredAt < toInstant);
        }

        return await query
            .OrderByDescending(a => a.OccurredAt)
            .Take(MaxRows)
            .Select(a => new AuditEventDto(
                a.Id, a.OccurredAt, a.Action, a.UserName, a.UserId, a.IpAddress,
                a.TenantId == null
                    ? null
                    : _db.Tenants.Where(t => t.Id == a.TenantId)
                        .Select(t => t.Name).FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
