using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Audit;
using SchoolErp.Domain.Auth;
using SchoolErp.Domain.Common;
using SchoolErp.Domain.Communication;
using SchoolErp.Domain.Homework;
using SchoolErp.Domain.Hostel;
using SchoolErp.Domain.Library;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Fees;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Staff;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Domain.Timetable;
using SchoolErp.Domain.Transport;
using SchoolErp.Infrastructure.Identity;

namespace SchoolErp.Infrastructure.Persistence;

/// <summary>
/// The application's single DbContext (business schema + ASP.NET Identity).
/// Applies two model-wide conventions:
/// <list type="bullet">
/// <item>Global query filters — soft-delete on every auditable entity, plus
/// tenant isolation on every <see cref="TenantEntity"/>.</item>
/// <item>Optimistic concurrency via PostgreSQL <c>xmin</c> on every entity.</item>
/// </list>
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>Platform-scoped tenant catalog.</summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>Academic sessions (tenant-scoped).</summary>
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    /// <summary>Grades/standards (tenant-scoped).</summary>
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();

    /// <summary>Class sections (tenant-scoped).</summary>
    public DbSet<Section> Sections => Set<Section>();

    /// <summary>Students (tenant-scoped).</summary>
    public DbSet<Student> Students => Set<Student>();

    /// <summary>Guardians (tenant-scoped).</summary>
    public DbSet<Guardian> Guardians => Set<Guardian>();

    /// <summary>Student↔guardian links (tenant-scoped).</summary>
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();

    /// <summary>Year-wise placements (tenant-scoped).</summary>
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    /// <summary>Daily attendance records (tenant-scoped).</summary>
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    /// <summary>Subjects (tenant-scoped).</summary>
    public DbSet<Subject> Subjects => Set<Subject>();

    /// <summary>Examinations (tenant-scoped).</summary>
    public DbSet<Exam> Exams => Set<Exam>();

    /// <summary>Scheduled papers (tenant-scoped).</summary>
    public DbSet<ExamSubject> ExamSubjects => Set<ExamSubject>();

    /// <summary>Mark entries (tenant-scoped).</summary>
    public DbSet<MarkEntry> MarkEntries => Set<MarkEntry>();

    /// <summary>Fee heads (tenant-scoped).</summary>
    public DbSet<FeeHead> FeeHeads => Set<FeeHead>();

    /// <summary>Fee structure items (tenant-scoped).</summary>
    public DbSet<FeeStructureItem> FeeStructureItems => Set<FeeStructureItem>();

    /// <summary>Fee payments (tenant-scoped).</summary>
    public DbSet<FeePayment> FeePayments => Set<FeePayment>();

    public DbSet<FeeConcession> FeeConcessions => Set<FeeConcession>();

    public DbSet<Domain.Notifications.PushToken> PushTokens => Set<Domain.Notifications.PushToken>();

    public DbSet<TermReport> TermReports => Set<TermReport>();

    public DbSet<TermReportComponent> TermReportComponents => Set<TermReportComponent>();

    public DbSet<TermStudentInput> TermStudentInputs => Set<TermStudentInput>();

    public DbSet<Domain.Communication.StudentMessage> StudentMessages => Set<Domain.Communication.StudentMessage>();

    public DbSet<Domain.Timetable.TimetableSubstitution> TimetableSubstitutions => Set<Domain.Timetable.TimetableSubstitution>();

    /// <summary>Online payment orders (platform-scoped, webhook-read).</summary>
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();

    /// <summary>Notices/circulars (tenant-scoped).</summary>
    public DbSet<Notice> Notices => Set<Notice>();

    /// <summary>Homework assignments (tenant-scoped).</summary>
    public DbSet<HomeworkAssignment> HomeworkAssignments => Set<HomeworkAssignment>();

    /// <summary>Vehicles (tenant-scoped).</summary>
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    /// <summary>Transport routes (tenant-scoped).</summary>
    public DbSet<TransportRoute> TransportRoutes => Set<TransportRoute>();

    /// <summary>Route stops (tenant-scoped).</summary>
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();

    /// <summary>Student transport allocations (tenant-scoped).</summary>
    public DbSet<StudentTransportAssignment> StudentTransportAssignments =>
        Set<StudentTransportAssignment>();

    /// <summary>Bus trips (tenant-scoped).</summary>
    public DbSet<Trip> Trips => Set<Trip>();

    /// <summary>Trip GPS pings (tenant-scoped).</summary>
    public DbSet<TripLocation> TripLocations => Set<TripLocation>();

    /// <summary>Per-student trip events (tenant-scoped).</summary>
    public DbSet<TripStudentEvent> TripStudentEvents => Set<TripStudentEvent>();

    /// <summary>Teaching staff (tenant-scoped).</summary>
    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<Domain.Leave.LeaveRequest> LeaveRequests => Set<Domain.Leave.LeaveRequest>();

    /// <summary>Admission enquiries pipeline (tenant-scoped).</summary>
    public DbSet<Domain.Admissions.AdmissionEnquiry> AdmissionEnquiries =>
        Set<Domain.Admissions.AdmissionEnquiry>();

    /// <summary>Timetable entries (tenant-scoped).</summary>
    public DbSet<TimetableEntry> TimetableEntries => Set<TimetableEntry>();

    /// <summary>Library books (tenant-scoped).</summary>
    public DbSet<Book> Books => Set<Book>();

    /// <summary>Book loans (tenant-scoped).</summary>
    public DbSet<BookLoan> BookLoans => Set<BookLoan>();

    /// <summary>Hostel buildings (tenant-scoped).</summary>
    public DbSet<HostelBuilding> Hostels => Set<HostelBuilding>();

    /// <summary>Hostel rooms (tenant-scoped).</summary>
    public DbSet<HostelRoom> HostelRooms => Set<HostelRoom>();

    /// <summary>Hostel stays (tenant-scoped).</summary>
    public DbSet<HostelAllocation> HostelAllocations => Set<HostelAllocation>();

    /// <summary>Action audit trail (platform-scoped, nullable tenant column).</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Transactional outbox (platform-scoped, dispatcher-read).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Rotating refresh tokens (platform-scoped, hash-lookup only).</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>One-time SMS login codes (platform-scoped, hash-lookup only).</summary>
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();

    /// <summary>
    /// Current tenant for query-filter composition. Must be a property (not a
    /// captured local) so EF parameterizes it per query execution.
    /// </summary>
    private Guid CurrentTenantId => _tenantContext.HasTenant ? _tenantContext.TenantId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // --- Identity multi-tenancy adjustments ---------------------------
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.FullName).HasMaxLength(128);
            // Same email/phone may exist in different schools, never twice in one.
            b.HasIndex(u => new { u.TenantId, u.NormalizedEmail }).IsUnique();
            b.HasIndex(u => new { u.TenantId, u.PhoneNumber }).IsUnique();
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.Property(r => r.Description).HasMaxLength(256);
            // Replace Identity's global unique RoleNameIndex with per-tenant
            // uniqueness — every school has its own "Teacher" role.
            b.HasIndex(r => r.NormalizedName).IsUnique(false)
                .HasDatabaseName("RoleNameIndex");
            b.HasIndex(r => new { r.TenantId, r.NormalizedName }).IsUnique();
        });

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
            {
                continue;
            }

            // xmin-backed optimistic concurrency (no extra storage cost).
            builder.Entity(entityType.ClrType)
                .Property<uint>(nameof(AuditableEntity.RowVersion))
                .IsRowVersion();

            builder.Entity(entityType.ClrType)
                .HasQueryFilter(BuildQueryFilter(entityType.ClrType));

            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Every tenant table is queried by tenant first; make that cheap.
                builder.Entity(entityType.ClrType)
                    .HasIndex(nameof(TenantEntity.TenantId));
            }
        }
    }

    /// <summary>
    /// Builds <c>e =&gt; !e.IsDeleted</c> for platform entities and
    /// <c>e =&gt; !e.IsDeleted &amp;&amp; e.TenantId == CurrentTenantId</c> for tenant entities.
    /// </summary>
    private LambdaExpression BuildQueryFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");

        Expression body = Expression.Not(
            Expression.Property(parameter, nameof(AuditableEntity.IsDeleted)));

        if (typeof(TenantEntity).IsAssignableFrom(clrType))
        {
            var currentTenantProperty = typeof(AppDbContext).GetProperty(
                nameof(CurrentTenantId), BindingFlags.NonPublic | BindingFlags.Instance)!;
            var tenantMatches = Expression.Equal(
                Expression.Property(parameter, nameof(TenantEntity.TenantId)),
                Expression.Property(Expression.Constant(this), currentTenantProperty));
            body = Expression.AndAlso(body, tenantMatches);
        }

        return Expression.Lambda(body, parameter);
    }
}
