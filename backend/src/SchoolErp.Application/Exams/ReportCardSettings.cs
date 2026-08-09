using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.TenantCatalog;

namespace SchoolErp.Application.Exams;

/// <summary>How this school's report cards are laid out.</summary>
public sealed record ReportCardSettingsDto(
    ReportCardTemplate Template,
    bool ShowAttendance,
    bool ShowRemarks,
    IReadOnlyList<string> Signatories);

/// <summary>The signed-in school's report-card settings.</summary>
public sealed record GetReportCardSettingsQuery : IRequest<ReportCardSettingsDto>;

/// <summary>Replaces them.</summary>
public sealed record UpdateReportCardSettingsCommand(
    ReportCardTemplate Template,
    bool ShowAttendance,
    bool ShowRemarks,
    IReadOnlyList<string> Signatories) : IRequest<ReportCardSettingsDto>;

/// <summary>Signature lines are printed side by side, so keep the row short.</summary>
public sealed class UpdateReportCardSettingsCommandValidator
    : AbstractValidator<UpdateReportCardSettingsCommand>
{
    public UpdateReportCardSettingsCommandValidator()
    {
        RuleFor(c => c.Template).IsInEnum();
        RuleFor(c => c.Signatories).NotNull()
            .Must(s => s.Count <= 4)
            .WithMessage("At most four signature lines fit across the page.");
        RuleForEach(c => c.Signatories).NotEmpty().MaximumLength(40);
    }
}

/// <summary>Defaults used when a school has never touched the settings.</summary>
public static class ReportCardDefaults
{
    public static readonly IReadOnlyList<string> Signatories =
        ["Class teacher", "Principal", "Parent / Guardian"];

    /// <summary>Splits the stored CSV, falling back to the defaults.</summary>
    public static IReadOnlyList<string> ParseSignatories(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return Signatories;
        }

        var parsed = stored.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                       StringSplitOptions.TrimEntries);
        return parsed.Length == 0 ? Signatories : parsed;
    }
}

/// <summary>Reads the current school's settings.</summary>
public sealed class GetReportCardSettingsQueryHandler
    : IRequestHandler<GetReportCardSettingsQuery, ReportCardSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetReportCardSettingsQueryHandler(
        IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ReportCardSettingsDto> Handle(
        GetReportCardSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == _tenantContext.TenantId)
            .Select(t => new
            {
                t.ReportCardTemplate,
                t.ReportCardShowAttendance,
                t.ReportCardShowRemarks,
                t.ReportCardSignatories,
            })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ReportCardSettingsDto(
            tenant.ReportCardTemplate,
            tenant.ReportCardShowAttendance,
            tenant.ReportCardShowRemarks,
            ReportCardDefaults.ParseSignatories(tenant.ReportCardSignatories));
    }
}

/// <summary>Writes them back to the school's own tenant row.</summary>
public sealed class UpdateReportCardSettingsCommandHandler
    : IRequestHandler<UpdateReportCardSettingsCommand, ReportCardSettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpdateReportCardSettingsCommandHandler(
        IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ReportCardSettingsDto> Handle(
        UpdateReportCardSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .FirstAsync(t => t.Id == _tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);

        var signatories = request.Signatories
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        tenant.ReportCardTemplate = request.Template;
        tenant.ReportCardShowAttendance = request.ShowAttendance;
        tenant.ReportCardShowRemarks = request.ShowRemarks;
        // Blank means "use the defaults", so store null rather than "".
        tenant.ReportCardSignatories = signatories.Count == 0
            ? null
            : string.Join(',', signatories);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ReportCardSettingsDto(
            tenant.ReportCardTemplate,
            tenant.ReportCardShowAttendance,
            tenant.ReportCardShowRemarks,
            ReportCardDefaults.ParseSignatories(tenant.ReportCardSignatories));
    }
}
