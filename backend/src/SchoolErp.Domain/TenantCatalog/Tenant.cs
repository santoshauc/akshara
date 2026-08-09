using SchoolErp.Domain.Common;

namespace SchoolErp.Domain.TenantCatalog;

/// <summary>
/// A school (tenant) in the platform catalog. This aggregate is platform-scoped,
/// not tenant-scoped: it inherits <see cref="AuditableEntity"/> directly and is
/// only readable by Super Admin and the tenant-resolution pipeline.
/// </summary>
public class Tenant : AuditableEntity
{
    /// <summary>Short unique school code parents type into the mobile apps (e.g. "GRWD01").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Registered school name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique subdomain under the platform domain (e.g. "greenwood" → greenwood.app.com).</summary>
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>Optional fully-qualified custom domain (e.g. "portal.greenwood.edu.in").</summary>
    public string? CustomDomain { get; set; }

    // ----- Contact & address -----
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    /// <summary>Board affiliation (CBSE / ICSE / State) and affiliation number.</summary>
    public string? AffiliationBoard { get; set; }
    public string? AffiliationNumber { get; set; }

    // ----- Branding -----
    public string? LogoUrl { get; set; }
    public string? ThemePrimaryColor { get; set; }
    public string? ThemeSecondaryColor { get; set; }

    // ----- Subscription & entitlements -----
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Trial;
    public DateOnly? SubscriptionExpiresOn { get; set; }
    /// <summary>Feature modules enabled for this school.</summary>
    public TenantModules EnabledModules { get; set; } = TenantModules.Core;
    public int StorageLimitMb { get; set; } = 5_120;
    public int SmsCredits { get; set; }

    /// <summary>
    /// Prefer WhatsApp for parent notifications; SMS stays the fallback when a
    /// WhatsApp send fails. Platform-controlled per school (WhatsApp conversation
    /// pricing differs from SMS credits).
    /// </summary>
    public bool WhatsAppEnabled { get; set; }

    // ----- Report cards -----
    /// <summary>Which columns the printed report card shows.</summary>
    public ReportCardTemplate ReportCardTemplate { get; set; } = ReportCardTemplate.MarksAndGrades;

    /// <summary>Print the year's attendance line under the marks table.</summary>
    public bool ReportCardShowAttendance { get; set; }

    /// <summary>Print a ruled box for the class teacher's handwritten remarks.</summary>
    public bool ReportCardShowRemarks { get; set; }

    /// <summary>
    /// Comma-separated signature lines, in print order. Null/blank falls back
    /// to class teacher, principal and guardian.
    /// </summary>
    public string? ReportCardSignatories { get; set; }

    // ----- Regional settings -----
    /// <summary>IANA timezone id; Indian schools default to Asia/Kolkata.</summary>
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    /// <summary>Default UI culture; "en" or "te" (Telugu).</summary>
    public string DefaultLanguage { get; set; } = "en";

    // ----- Lifecycle -----
    public TenantStatus Status { get; set; } = TenantStatus.Provisioning;

    /// <summary>True when the tenant may serve traffic.</summary>
    public bool IsActive => Status == TenantStatus.Active && !IsDeleted;
}
