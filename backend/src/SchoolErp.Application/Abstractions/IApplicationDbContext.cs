using Microsoft.EntityFrameworkCore;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Audit;
using SchoolErp.Domain.Communication;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Fees;
using SchoolErp.Domain.Homework;
using SchoolErp.Domain.Hostel;
using SchoolErp.Domain.Library;
using SchoolErp.Domain.Outbox;
using SchoolErp.Domain.Staff;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Domain.Timetable;
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

    DbSet<FeeConcession> FeeConcessions { get; }

    DbSet<Domain.Notifications.PushToken> PushTokens { get; }

    DbSet<TermReport> TermReports { get; }

    DbSet<TermReportComponent> TermReportComponents { get; }

    DbSet<TermStudentInput> TermStudentInputs { get; }

    DbSet<Domain.Communication.StudentMessage> StudentMessages { get; }

    DbSet<Domain.Timetable.TimetableSubstitution> TimetableSubstitutions { get; }

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

    DbSet<Teacher> Teachers { get; }

    DbSet<Domain.Leave.LeaveRequest> LeaveRequests { get; }

    DbSet<Domain.Admissions.AdmissionEnquiry> AdmissionEnquiries { get; }

    DbSet<TimetableEntry> TimetableEntries { get; }

    DbSet<Book> Books { get; }

    DbSet<BookLoan> BookLoans { get; }

    DbSet<Domain.Campuses.Campus> Campuses { get; }

    /// <summary>Colleges only; a school's set is empty.</summary>
    DbSet<Department> Departments { get; }

    /// <inheritdoc cref="Departments"/>
    DbSet<Programme> Programmes { get; }

    DbSet<Domain.Inventory.InventoryItem> InventoryItems { get; }

    DbSet<Domain.Inventory.StockMovement> StockMovements { get; }

    DbSet<Domain.FrontOffice.VisitorEntry> VisitorEntries { get; }

    DbSet<Domain.FrontOffice.GatePass> GatePasses { get; }

    DbSet<HostelBuilding> Hostels { get; }

    DbSet<HostelRoom> HostelRooms { get; }

    DbSet<HostelAllocation> HostelAllocations { get; }

    DbSet<AuditEvent> AuditEvents { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<Domain.Billing.Invoice> Invoices { get; }

    DbSet<Domain.Billing.InvoiceLine> InvoiceLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
