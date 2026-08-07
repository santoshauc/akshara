using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Attendance;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Outbox;

namespace SchoolErp.Application.Exams.Commands;

/// <summary>
/// Publishes an exam: freezes marks, makes results visible, and queues an SMS
/// to the primary guardian of every student with marks — via the outbox, in
/// the same transaction.
/// </summary>
public sealed record PublishExamCommand(Guid ExamId) : IRequest;

/// <summary>Publishes and fans out result notifications.</summary>
public sealed class PublishExamCommandHandler : IRequestHandler<PublishExamCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLookup _tenantLookup;

    public PublishExamCommandHandler(
        IApplicationDbContext db,
        ITenantContext tenantContext,
        ITenantLookup tenantLookup)
    {
        _db = db;
        _tenantContext = tenantContext;
        _tenantLookup = tenantLookup;
    }

    public async Task Handle(PublishExamCommand request, CancellationToken cancellationToken)
    {
        var exam = await _db.Exams
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Exam), request.ExamId);

        if (exam.Status == ExamStatus.Published)
        {
            throw new ConflictException("The exam is already published.");
        }

        var hasMarks = await _db.MarkEntries
            .AnyAsync(m => m.ExamSubject!.ExamId == exam.Id, cancellationToken)
            .ConfigureAwait(false);
        if (!hasMarks)
        {
            throw new ConflictException("Cannot publish an exam with no marks entered.");
        }

        exam.Status = ExamStatus.Published;

        var tenant = await _tenantLookup.FindByIdAsync(_tenantContext.TenantId, cancellationToken)
            .ConfigureAwait(false);
        var schoolName = tenant?.Name ?? "your school";

        // Every student with at least one mark row → one SMS to the primary guardian.
        var recipients = await _db.MarkEntries
            .Where(m => m.ExamSubject!.ExamId == exam.Id)
            .Select(m => m.StudentId)
            .Distinct()
            .Join(_db.StudentGuardians.Where(sg => sg.IsPrimary && sg.Guardian != null),
                studentId => studentId, sg => sg.StudentId,
                (studentId, sg) => new
                {
                    sg.Guardian!.Phone,
                    StudentName = _db.Students.Where(s => s.Id == studentId)
                        .Select(s => s.FirstName).First(),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var recipient in recipients)
        {
            var payload = new SmsPayload(
                recipient.Phone,
                $"Results for {exam.Name} are now available for {recipient.StudentName} " +
                $"at {schoolName}. Open the parent app to view the report card.");

            _db.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = _tenantContext.TenantId,
                Type = OutboxMessageTypes.Sms,
                Payload = JsonSerializer.Serialize(payload),
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
