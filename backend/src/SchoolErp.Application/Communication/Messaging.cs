using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Communication;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Communication;

/// <summary>One message as shown in a conversation.</summary>
public sealed record StudentMessageDto(
    Guid Id,
    bool SentByStaff,
    string SenderName,
    string Body,
    DateTimeOffset SentAt,
    bool Read);

/// <summary>One row of the staff inbox: a student's thread with unread count.</summary>
public sealed record MessageThreadDto(
    Guid StudentId,
    string StudentName,
    string? ClassName,
    string LastMessage,
    DateTimeOffset LastMessageAt,
    int UnreadForStaff);

/// <summary>
/// Sends a message on a student's thread. The caller's side is explicit —
/// controllers set it from their own authorization context, never from input.
/// </summary>
public sealed record SendStudentMessageCommand(
    Guid StudentId, string Body, bool SentByStaff) : IRequest<Guid>;

/// <summary>Body shape rules.</summary>
public sealed class SendStudentMessageCommandValidator
    : AbstractValidator<SendStudentMessageCommand>
{
    public SendStudentMessageCommandValidator() =>
        RuleFor(c => c.Body).NotEmpty().MaximumLength(2048)
            .Must(b => !string.IsNullOrWhiteSpace(b))
            .WithMessage("A message needs some text.");
}

/// <summary>Appends the message with a sender-name snapshot.</summary>
public sealed class SendStudentMessageCommandHandler
    : IRequestHandler<SendStudentMessageCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SendStudentMessageCommandHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        SendStudentMessageCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(Student), request.StudentId);
        }

        var message = new StudentMessage
        {
            StudentId = request.StudentId,
            SentByStaff = request.SentByStaff,
            SenderUserId = Guid.TryParse(_currentUser.UserId, out var userId)
                ? userId
                : Guid.Empty,
            SenderName = _currentUser.UserName ?? (request.SentByStaff ? "School" : "Parent"),
            Body = request.Body.Trim(),
        };
        _db.StudentMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return message.Id;
    }
}

/// <summary>
/// A student's conversation, oldest first. Reading as one side marks the
/// OTHER side's messages read — that's what "opened the thread" means.
/// </summary>
public sealed record GetStudentMessagesQuery(
    Guid StudentId, bool AsStaff, int Take = 50) : IRequest<IReadOnlyList<StudentMessageDto>>;

/// <summary>List + mark-read handler.</summary>
public sealed class GetStudentMessagesQueryHandler
    : IRequestHandler<GetStudentMessagesQuery, IReadOnlyList<StudentMessageDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetStudentMessagesQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<StudentMessageDto>> Handle(
        GetStudentMessagesQuery request, CancellationToken cancellationToken)
    {
        var messages = await _db.StudentMessages
            .Where(m => m.StudentId == request.StudentId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(request.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _clock.GetUtcNow();
        var changed = false;
        foreach (var message in messages)
        {
            if (request.AsStaff && !message.SentByStaff && message.ReadByStaffAt is null)
            {
                message.ReadByStaffAt = now;
                changed = true;
            }
            else if (!request.AsStaff && message.SentByStaff && message.ReadByParentAt is null)
            {
                message.ReadByParentAt = now;
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new StudentMessageDto(
                m.Id,
                m.SentByStaff,
                m.SenderName,
                m.Body,
                m.CreatedAt,
                m.SentByStaff ? m.ReadByParentAt != null : m.ReadByStaffAt != null))
            .ToList();
    }
}

/// <summary>The staff inbox: every thread, unread-first then newest.</summary>
public sealed record GetMessageThreadsQuery : IRequest<IReadOnlyList<MessageThreadDto>>;

/// <summary>Groups messages per student.</summary>
public sealed class GetMessageThreadsQueryHandler
    : IRequestHandler<GetMessageThreadsQuery, IReadOnlyList<MessageThreadDto>>
{
    private readonly IApplicationDbContext _db;

    public GetMessageThreadsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<MessageThreadDto>> Handle(
        GetMessageThreadsQuery request, CancellationToken cancellationToken)
    {
        var threads = await _db.StudentMessages.AsNoTracking()
            .GroupBy(m => m.StudentId)
            .Select(g => new
            {
                StudentId = g.Key,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).First().Body,
                LastMessageAt = g.Max(m => m.CreatedAt),
                UnreadForStaff = g.Count(m => !m.SentByStaff && m.ReadByStaffAt == null),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var studentIds = threads.Select(t => t.StudentId).ToList();
        var students = await _db.Students.AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                Name = (s.FirstName + " " + s.LastName).Trim(),
                ClassName = _db.Enrollments
                    .Where(e => e.StudentId == s.Id && e.Status == EnrollmentStatus.Active)
                    .Select(e => e.SchoolClass!.Name + " " +
                        (e.Section != null ? e.Section.Name : ""))
                    .FirstOrDefault(),
            })
            .ToDictionaryAsync(s => s.Id, cancellationToken)
            .ConfigureAwait(false);

        return threads
            .OrderByDescending(t => t.UnreadForStaff > 0)
            .ThenByDescending(t => t.LastMessageAt)
            .Select(t => new MessageThreadDto(
                t.StudentId,
                students.TryGetValue(t.StudentId, out var s) ? s.Name : "Student",
                students.TryGetValue(t.StudentId, out var s2) ? s2.ClassName?.Trim() : null,
                t.LastMessage,
                t.LastMessageAt,
                t.UnreadForStaff))
            .ToList();
    }
}

/// <summary>Unread staff-message count for one student (parent app badge).</summary>
public sealed record GetUnreadForParentQuery(Guid StudentId) : IRequest<int>;

/// <summary>Count handler.</summary>
public sealed class GetUnreadForParentQueryHandler : IRequestHandler<GetUnreadForParentQuery, int>
{
    private readonly IApplicationDbContext _db;

    public GetUnreadForParentQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<int> Handle(GetUnreadForParentQuery request, CancellationToken cancellationToken) =>
        _db.StudentMessages.AsNoTracking()
            .CountAsync(m => m.StudentId == request.StudentId &&
                             m.SentByStaff && m.ReadByParentAt == null,
                cancellationToken);
}
