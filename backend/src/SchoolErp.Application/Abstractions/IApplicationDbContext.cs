using Microsoft.EntityFrameworkCore;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Communication;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Fees;
using SchoolErp.Domain.Homework;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Domain.Transport;

namespace SchoolErp.Application.Abstractions;

/// <summary>
/// Data-access surface for CQRS handlers. Exposes EF Core DbSets directly
/// (pragmatic Clean Architecture) while keeping the concrete context — with
/// its tenancy interceptors and RLS wiring — in Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<AcademicYear> AcademicYears { get; }

    DbSet<SchoolClass> SchoolClasses { get; }

    DbSet<Section> Sections { get; }

    DbSet<Student> Students { get; }

    DbSet<Guardian> Guardians { get; }

    DbSet<StudentGuardian> StudentGuardians { get; }

    DbSet<Enrollment> Enrollments { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }

    DbSet<Subject> Subjects { get; }

    DbSet<Exam> Exams { get; }

    DbSet<ExamSubject> ExamSubjects { get; }

    DbSet<MarkEntry> MarkEntries { get; }

    DbSet<FeeHead> FeeHeads { get; }

    DbSet<FeeStructureItem> FeeStructureItems { get; }

    DbSet<FeePayment> FeePayments { get; }

    DbSet<PaymentOrder> PaymentOrders { get; }

    DbSet<Notice> Notices { get; }

    DbSet<HomeworkAssignment> HomeworkAssignments { get; }

    DbSet<Vehicle> Vehicles { get; }

    DbSet<TransportRoute> TransportRoutes { get; }

    DbSet<RouteStop> RouteStops { get; }

    DbSet<StudentTransportAssignment> StudentTransportAssignments { get; }

    DbSet<Trip> Trips { get; }

    DbSet<TripLocation> TripLocations { get; }

    DbSet<TripStudentEvent> TripStudentEvents { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
