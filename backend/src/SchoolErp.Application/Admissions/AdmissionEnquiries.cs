using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Admissions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Admissions;

/// <summary>One enquiry row for the pipeline board.</summary>
public sealed record EnquiryDto(
    Guid Id,
    string ChildName,
    DateOnly? DateOfBirth,
    string AppliedClass,
    string ParentName,
    string Phone,
    string? Email,
    EnquirySource Source,
    EnquiryStatus Status,
    DateOnly? FollowUpOn,
    bool FollowUpDue,
    string? Notes,
    Guid? StudentId,
    DateTimeOffset CreatedAt);

/// <summary>Registers a fresh enquiry at first contact.</summary>
public sealed record CreateEnquiryCommand(
    string ChildName,
    DateOnly? DateOfBirth,
    string AppliedClass,
    string ParentName,
    string Phone,
    string? Email,
    EnquirySource Source,
    DateOnly? FollowUpOn,
    string? Notes) : IRequest<Guid>;

/// <summary>Contact and shape rules for a new enquiry.</summary>
public sealed class CreateEnquiryCommandValidator : AbstractValidator<CreateEnquiryCommand>
{
    public CreateEnquiryCommandValidator()
    {
        RuleFor(c => c.ChildName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("The child's name is required.")
            .MaximumLength(200);
        RuleFor(c => c.AppliedClass)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("The class applied for is required.")
            .MaximumLength(100);
        RuleFor(c => c.ParentName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("The parent's name is required.")
            .MaximumLength(200);
        RuleFor(c => c.Phone)
            .NotEmpty()
            .Matches(@"^\+?[0-9]{10,15}$")
            .WithMessage("The phone number must be 10–15 digits, optionally starting with +.");
        RuleFor(c => c.Email).EmailAddress().MaximumLength(320)
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Source).IsInEnum();
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

/// <summary>Creates the enquiry in status New.</summary>
public sealed class CreateEnquiryCommandHandler : IRequestHandler<CreateEnquiryCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateEnquiryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateEnquiryCommand request, CancellationToken cancellationToken)
    {
        var enquiry = new AdmissionEnquiry
        {
            ChildName = request.ChildName.Trim(),
            DateOfBirth = request.DateOfBirth,
            AppliedClass = request.AppliedClass.Trim(),
            ParentName = request.ParentName.Trim(),
            Phone = request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Source = request.Source,
            FollowUpOn = request.FollowUpOn,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        _db.AdmissionEnquiries.Add(enquiry);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return enquiry.Id;
    }
}

/// <summary>
/// Moves an enquiry through the pipeline and/or reschedules the follow-up.
/// Admitted is reserved for <see cref="ConvertEnquiryCommand"/> so the funnel
/// only counts conversions that produced a real student.
/// </summary>
public sealed record UpdateEnquiryCommand(
    Guid EnquiryId,
    EnquiryStatus Status,
    DateOnly? FollowUpOn,
    string? Notes) : IRequest;

/// <summary>Pipeline transition rules.</summary>
public sealed class UpdateEnquiryCommandValidator : AbstractValidator<UpdateEnquiryCommand>
{
    public UpdateEnquiryCommandValidator()
    {
        RuleFor(c => c.Status).IsInEnum()
            .Must(s => s != EnquiryStatus.Admitted)
            .WithMessage("Use the convert action to admit an enquiry.");
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}

/// <summary>Applies the stage change.</summary>
public sealed class UpdateEnquiryCommandHandler : IRequestHandler<UpdateEnquiryCommand>
{
    private readonly IApplicationDbContext _db;

    public UpdateEnquiryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateEnquiryCommand request, CancellationToken cancellationToken)
    {
        var enquiry = await _db.AdmissionEnquiries
            .FirstOrDefaultAsync(e => e.Id == request.EnquiryId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(AdmissionEnquiry), request.EnquiryId);

        if (enquiry.Status == EnquiryStatus.Admitted)
        {
            throw new ConflictException("An admitted enquiry can no longer be edited.");
        }

        enquiry.Status = request.Status;
        enquiry.FollowUpOn = request.FollowUpOn;
        enquiry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Stamps an enquiry Admitted and links the student created from it — called
/// right after the admission succeeds so the funnel stays measurable.
/// </summary>
public sealed record ConvertEnquiryCommand(Guid EnquiryId, Guid StudentId) : IRequest;

/// <summary>Links the admitted student.</summary>
public sealed class ConvertEnquiryCommandHandler : IRequestHandler<ConvertEnquiryCommand>
{
    private readonly IApplicationDbContext _db;

    public ConvertEnquiryCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(ConvertEnquiryCommand request, CancellationToken cancellationToken)
    {
        var enquiry = await _db.AdmissionEnquiries
            .FirstOrDefaultAsync(e => e.Id == request.EnquiryId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(AdmissionEnquiry), request.EnquiryId);

        if (enquiry.Status == EnquiryStatus.Admitted)
        {
            throw new ConflictException("This enquiry has already been converted.");
        }

        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException(nameof(Student), request.StudentId);
        }

        enquiry.Status = EnquiryStatus.Admitted;
        enquiry.StudentId = request.StudentId;
        enquiry.FollowUpOn = null; // Nothing left to chase.
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// The pipeline board: optionally filtered by status, follow-ups-due first,
/// then newest first.
/// </summary>
public sealed record GetEnquiriesQuery(EnquiryStatus? Status) : IRequest<List<EnquiryDto>>;

/// <summary>Reads the board for the current tenant.</summary>
public sealed class GetEnquiriesQueryHandler : IRequestHandler<GetEnquiriesQuery, List<EnquiryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetEnquiriesQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<List<EnquiryDto>> Handle(
        GetEnquiriesQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var rows = await _db.AdmissionEnquiries
            .Where(e => request.Status == null || e.Status == request.Status)
            .OrderByDescending(e => e.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(e => new EnquiryDto(
                e.Id, e.ChildName, e.DateOfBirth, e.AppliedClass, e.ParentName,
                e.Phone, e.Email, e.Source, e.Status, e.FollowUpOn,
                FollowUpDue: e.FollowUpOn is { } due && due <= today &&
                             e.Status is not (EnquiryStatus.Admitted or EnquiryStatus.Lost),
                e.Notes, e.StudentId, e.CreatedAt))
            .OrderByDescending(e => e.FollowUpDue)
            .ThenByDescending(e => e.CreatedAt)
            .ToList();
    }
}
