using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Shared.Models;

namespace SchoolErp.Application.TenantCatalog.Queries;

/// <summary>Paged, searchable school listing for the Super Admin console.</summary>
public sealed record GetTenantsQuery(
    string? Search = null,
    TenantStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<TenantDto>>;

/// <summary>Pagination bounds.</summary>
public sealed class GetTenantsQueryValidator : AbstractValidator<GetTenantsQuery>
{
    public GetTenantsQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>Projects directly to DTOs in SQL; no entities are materialized.</summary>
public sealed class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, PagedResult<TenantDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTenantsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<TenantDto>> Handle(
        GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Tenants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = $"%{request.Search.Trim()}%";
            query = query.Where(t =>
                EF.Functions.ILike(t.Name, term) ||
                EF.Functions.ILike(t.Code, term) ||
                EF.Functions.ILike(t.Subdomain, term));
        }

        if (request.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(TenantMappings.Projection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<TenantDto>(items, total, request.Page, request.PageSize);
    }
}

/// <summary>Single-school detail lookup.</summary>
public sealed record GetTenantByIdQuery(Guid Id) : IRequest<TenantDto>;

/// <summary>Returns the school or 404s.</summary>
public sealed class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto>
{
    private readonly IApplicationDbContext _db;

    public GetTenantByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TenantDto> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(TenantMappings.Projection)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return dto ?? throw new NotFoundException(nameof(Tenant), request.Id);
    }
}
