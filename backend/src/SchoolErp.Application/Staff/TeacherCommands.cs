using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Staff;

namespace SchoolErp.Application.Staff;

/// <summary>A teacher as listed and edited by school staff.</summary>
public sealed record TeacherDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Phone,
    string? Email,
    string? Qualification,
    string? Specialization,
    DateOnly? JoinedOn,
    bool IsActive,
    bool HasLogin);

/// <summary>Registers a teacher. Employee code and phone are unique per school.</summary>
public sealed record CreateTeacherCommand(
    string EmployeeCode,
    string FullName,
    string Phone,
    string? Email,
    string? Qualification,
    string? Specialization,
    DateOnly? JoinedOn) : IRequest<Guid>;

/// <summary>Shape rules for teacher registration.</summary>
public sealed class CreateTeacherCommandValidator : AbstractValidator<CreateTeacherCommand>
{
    public CreateTeacherCommandValidator()
    {
        RuleFor(c => c.EmployeeCode).NotEmpty().MaximumLength(32);
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Phone).NotEmpty().Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("Phone must be a valid 10–15 digit number.");
        RuleFor(c => c.Email).EmailAddress().MaximumLength(256)
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Qualification).MaximumLength(256);
        RuleFor(c => c.Specialization).MaximumLength(256);
    }
}

/// <summary>Rejects duplicate employee codes and phones with a clear 409.</summary>
public sealed class CreateTeacherCommandHandler : IRequestHandler<CreateTeacherCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateTeacherCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateTeacherCommand request, CancellationToken cancellationToken)
    {
        var code = request.EmployeeCode.Trim();
        var phone = request.Phone.Trim();

        if (await _db.Teachers.AnyAsync(t => t.EmployeeCode == code, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"A teacher with employee code '{code}' already exists.");
        }

        if (await _db.Teachers.AnyAsync(t => t.Phone == phone, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException($"A teacher with phone '{phone}' already exists.");
        }

        var teacher = new Teacher
        {
            EmployeeCode = code,
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = request.Email?.Trim(),
            Qualification = request.Qualification?.Trim(),
            Specialization = request.Specialization?.Trim(),
            JoinedOn = request.JoinedOn,
        };
        _db.Teachers.Add(teacher);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return teacher.Id;
    }
}

/// <summary>Edits a teacher, including activation state.</summary>
public sealed record UpdateTeacherCommand(
    Guid TeacherId,
    string FullName,
    string Phone,
    string? Email,
    string? Qualification,
    string? Specialization,
    DateOnly? JoinedOn,
    bool IsActive) : IRequest;

/// <summary>Same shape rules as creation (employee code is immutable).</summary>
public sealed class UpdateTeacherCommandValidator : AbstractValidator<UpdateTeacherCommand>
{
    public UpdateTeacherCommandValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Phone).NotEmpty().Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("Phone must be a valid 10–15 digit number.");
        RuleFor(c => c.Email).EmailAddress().MaximumLength(256)
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Qualification).MaximumLength(256);
        RuleFor(c => c.Specialization).MaximumLength(256);
    }
}

/// <summary>Applies edits; keeps per-tenant phone uniqueness.</summary>
public sealed class UpdateTeacherCommandHandler : IRequestHandler<UpdateTeacherCommand>
{
    private readonly IApplicationDbContext _db;

    public UpdateTeacherCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateTeacherCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _db.Teachers
            .FirstOrDefaultAsync(t => t.Id == request.TeacherId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("Teacher", request.TeacherId);

        var phone = request.Phone.Trim();
        if (await _db.Teachers.AnyAsync(
                t => t.Id != teacher.Id && t.Phone == phone, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new ConflictException($"A teacher with phone '{phone}' already exists.");
        }

        teacher.FullName = request.FullName.Trim();
        teacher.Phone = phone;
        teacher.Email = request.Email?.Trim();
        teacher.Qualification = request.Qualification?.Trim();
        teacher.Specialization = request.Specialization?.Trim();
        teacher.JoinedOn = request.JoinedOn;
        teacher.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// One-click login for a teacher: creates a staff account with the school's
/// "Teacher" role from the teacher's own contact details. The teacher can then
/// sign in with the temporary password (or phone OTP) and mark attendance,
/// enter marks and manage homework.
/// </summary>
public sealed record CreateTeacherLoginCommand(
    Guid TeacherId, string TemporaryPassword) : IRequest<Guid>;

/// <summary>Password shape rule (Identity enforces complexity on top).</summary>
public sealed class CreateTeacherLoginCommandValidator
    : AbstractValidator<CreateTeacherLoginCommand>
{
    public CreateTeacherLoginCommandValidator() =>
        RuleFor(c => c.TemporaryPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
}

/// <summary>Delegates to the identity layer (role + account + link).</summary>
public sealed class CreateTeacherLoginCommandHandler
    : IRequestHandler<CreateTeacherLoginCommand, Guid>
{
    private readonly Users.IUserAdminService _users;

    public CreateTeacherLoginCommandHandler(Users.IUserAdminService users) => _users = users;

    public Task<Guid> Handle(CreateTeacherLoginCommand request, CancellationToken cancellationToken) =>
        _users.CreateTeacherLoginAsync(request.TeacherId, request.TemporaryPassword, cancellationToken);
}

/// <summary>Teacher directory; optional name/code/phone search.</summary>
public sealed record GetTeachersQuery(string? Search = null)
    : IRequest<IReadOnlyList<TeacherDto>>;

/// <summary>Ordered by name; search matches name, code or phone.</summary>
public sealed class GetTeachersQueryHandler
    : IRequestHandler<GetTeachersQuery, IReadOnlyList<TeacherDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTeachersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TeacherDto>> Handle(
        GetTeachersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Teachers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t =>
                EF.Functions.ILike(t.FullName, $"%{term}%") ||
                EF.Functions.ILike(t.EmployeeCode, $"%{term}%") ||
                t.Phone.Contains(term));
        }

        return await query
            .OrderBy(t => t.FullName)
            .Select(t => new TeacherDto(
                t.Id, t.EmployeeCode, t.FullName, t.Phone, t.Email,
                t.Qualification, t.Specialization, t.JoinedOn, t.IsActive,
                t.UserId != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>One period on a teacher's weekly schedule, across classes.</summary>
public sealed record TeacherScheduleItemDto(
    int DayOfWeek,
    int Period,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SubjectName,
    string ClassName,
    string? SectionName,
    bool IsPublished);

/// <summary>A teacher's full weekly schedule (drafts included — staff view).</summary>
public sealed record GetTeacherScheduleQuery(Guid TeacherId)
    : IRequest<IReadOnlyList<TeacherScheduleItemDto>>;

/// <summary>Collects the teacher's slots from every class scope.</summary>
public sealed class GetTeacherScheduleQueryHandler
    : IRequestHandler<GetTeacherScheduleQuery, IReadOnlyList<TeacherScheduleItemDto>>
{
    private readonly IApplicationDbContext _db;

    public GetTeacherScheduleQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TeacherScheduleItemDto>> Handle(
        GetTeacherScheduleQuery request, CancellationToken cancellationToken)
    {
        if (!await _db.Teachers.AnyAsync(t => t.Id == request.TeacherId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Teacher", request.TeacherId);
        }

        return await _db.TimetableEntries.AsNoTracking()
            .Where(t => t.TeacherId == request.TeacherId)
            .OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime)
            .Select(t => new TeacherScheduleItemDto(
                t.DayOfWeek, t.Period, t.StartTime, t.EndTime,
                t.Subject!.Name,
                _db.SchoolClasses.Where(c => c.Id == t.SchoolClassId)
                    .Select(c => c.Name).First(),
                t.SectionId == null
                    ? null
                    : _db.Sections.Where(s => s.Id == t.SectionId)
                        .Select(s => s.Name).First(),
                t.IsPublished))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
