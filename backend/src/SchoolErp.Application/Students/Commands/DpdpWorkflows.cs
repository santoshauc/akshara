using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Students.Commands;

/// <summary>
/// DPDP data-access request: everything the school stores about one student,
/// as a single JSON document handed to the data principal.
/// </summary>
public sealed record ExportStudentDataQuery(Guid StudentId) : IRequest<byte[]>;

/// <summary>Collects each module's records for the student.</summary>
public sealed class ExportStudentDataQueryHandler : IRequestHandler<ExportStudentDataQuery, byte[]>
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public ExportStudentDataQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<byte[]> Handle(ExportStudentDataQuery request, CancellationToken ct)
    {
        var student = await _db.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var guardians = await _db.StudentGuardians.AsNoTracking()
            .Where(g => g.StudentId == request.StudentId)
            .Select(g => new
            {
                g.Guardian!.FirstName,
                g.Guardian.LastName,
                g.Guardian.Phone,
                g.Guardian.Email,
                Relation = g.Guardian.Relation.ToString(),
                g.IsPrimary,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var enrollments = await _db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == request.StudentId)
            .Select(e => new
            {
                Year = e.AcademicYear!.Name,
                Class = e.SchoolClass!.Name,
                Section = e.Section != null ? e.Section.Name : null,
                e.RollNumber,
                Status = e.Status.ToString(),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var attendance = await _db.AttendanceRecords.AsNoTracking()
            .Where(a => a.StudentId == request.StudentId)
            .OrderBy(a => a.Date)
            .Select(a => new { a.Date, a.Period, Status = a.Status.ToString(), a.Remarks })
            .ToListAsync(ct).ConfigureAwait(false);

        var payments = await _db.FeePayments.AsNoTracking()
            .Where(p => p.StudentId == request.StudentId)
            .Select(p => new
            {
                p.ReceiptNumber, p.Amount, p.PaidOn,
                Mode = p.Mode.ToString(), p.Reference,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var concessions = await _db.FeeConcessions.AsNoTracking()
            .Where(c => c.StudentId == request.StudentId)
            .Select(c => new { c.Amount, c.Reason })
            .ToListAsync(ct).ConfigureAwait(false);

        var loans = await _db.BookLoans.AsNoTracking()
            .Where(l => l.StudentId == request.StudentId)
            .Select(l => new { l.Book!.Title, l.IssuedOn, l.DueOn, l.ReturnedOn })
            .ToListAsync(ct).ConfigureAwait(false);

        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.StudentId == request.StudentId)
            .Select(l => new
            {
                l.FromDate, l.ToDate, l.Reason,
                Status = l.Status.ToString(), l.DecisionNote,
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var messages = await _db.StudentMessages.AsNoTracking()
            .Where(m => m.StudentId == request.StudentId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.SentByStaff, m.SenderName, m.Body, SentAt = m.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);

        var export = new
        {
            ExportedAt = _clock.GetUtcNow(),
            Notice = "Personal data export under the Digital Personal Data Protection Act (DPDP).",
            Profile = new
            {
                student.AdmissionNumber,
                student.FirstName,
                student.LastName,
                student.DateOfBirth,
                Gender = student.Gender.ToString(),
                student.BloodGroup,
                student.Email,
                student.Phone,
                student.AddressLine1,
                student.City,
                student.State,
                student.PostalCode,
                student.MedicalNotes,
                student.AdmissionDate,
                HasPhoto = student.PhotoUrl != null,
            },
            Guardians = guardians,
            Enrollments = enrollments,
            Attendance = attendance,
            FeePayments = payments,
            FeeConcessions = concessions,
            LibraryLoans = loans,
            LeaveRequests = leaves,
            Messages = messages,
        };

        return JsonSerializer.SerializeToUtf8Bytes(export, Pretty);
    }
}

/// <summary>
/// DPDP erasure request. Personal data is anonymized in place and the student
/// is soft-deleted (statutory academic records stay as anonymous rows).
/// Guardians left with no other active children are anonymized too. The
/// command itself lands in the audit trail like every other command.
/// </summary>
public sealed record EraseStudentDataCommand(Guid StudentId, string Reason) : IRequest;

/// <summary>Reason is mandatory — it is the paper trail.</summary>
public sealed class EraseStudentDataCommandValidator : AbstractValidator<EraseStudentDataCommand>
{
    public EraseStudentDataCommandValidator() =>
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(512);
}

/// <summary>Anonymize + soft-delete in one transaction.</summary>
public sealed class EraseStudentDataCommandHandler : IRequestHandler<EraseStudentDataCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _fileStorage;

    public EraseStudentDataCommandHandler(IApplicationDbContext db, IFileStorage fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    public async Task Handle(EraseStudentDataCommand request, CancellationToken ct)
    {
        var student = await _db.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, ct)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        // The stored photo is personal data — delete the file itself.
        const string filePrefix = "/api/v1/files/";
        if (student.PhotoUrl is { } photoUrl &&
            photoUrl.StartsWith(filePrefix, StringComparison.Ordinal))
        {
            await _fileStorage.DeleteAsync(photoUrl[filePrefix.Length..], ct)
                .ConfigureAwait(false);
        }

        student.FirstName = "Erased";
        student.LastName = "Student";
        student.DateOfBirth = new DateOnly(2000, 1, 1);
        student.BloodGroup = null;
        student.Email = null;
        student.Phone = null;
        student.AddressLine1 = null;
        student.City = null;
        student.State = null;
        student.PostalCode = null;
        student.PhotoUrl = null;
        student.MedicalNotes = null;
        student.Status = StudentStatus.Withdrawn;
        student.IsDeleted = true;

        // Guardians with no other active children get anonymized too.
        var guardianIds = await _db.StudentGuardians
            .Where(g => g.StudentId == request.StudentId)
            .Select(g => g.GuardianId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var guardianId in guardianIds)
        {
            var hasOtherChildren = await _db.StudentGuardians
                .AnyAsync(g => g.GuardianId == guardianId &&
                               g.StudentId != request.StudentId, ct)
                .ConfigureAwait(false);
            if (hasOtherChildren)
            {
                continue;
            }

            var guardian = await _db.Guardians
                .FirstOrDefaultAsync(g => g.Id == guardianId, ct)
                .ConfigureAwait(false);
            if (guardian is null)
            {
                continue;
            }

            // Push tokens registered against the guardian's phone die with it.
            var tokens = await _db.PushTokens
                .Where(t => t.Phone == guardian.Phone)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var token in tokens)
            {
                _db.PushTokens.Remove(token);
            }

            guardian.FirstName = "Erased";
            guardian.LastName = "Guardian";
            // Phone is unique per tenant — a random placeholder keeps the row valid.
            guardian.Phone = $"erased-{Guid.NewGuid():N}"[..20];
            guardian.Email = null;
            guardian.IsDeleted = true;
        }

        // Free-text that carries personal context gets redacted in place.
        var messages = await _db.StudentMessages
            .Where(m => m.StudentId == request.StudentId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var message in messages)
        {
            message.Body = "[erased]";
            message.SenderName = message.SentByStaff ? message.SenderName : "Erased Guardian";
        }

        var leaves = await _db.LeaveRequests
            .Where(l => l.StudentId == request.StudentId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var leave in leaves)
        {
            leave.Reason = "[erased]";
            leave.DecisionNote = null;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
