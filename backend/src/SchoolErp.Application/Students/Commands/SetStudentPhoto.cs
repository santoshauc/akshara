using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;

namespace SchoolErp.Application.Students.Commands;

/// <summary>
/// Points the student at a freshly stored photo. Returns the PREVIOUS photo
/// URL so the caller can delete the orphaned file after the commit.
/// </summary>
public sealed record SetStudentPhotoCommand(Guid StudentId, string PhotoUrl) : IRequest<string?>;

/// <summary>Swap handler.</summary>
public sealed class SetStudentPhotoCommandHandler : IRequestHandler<SetStudentPhotoCommand, string?>
{
    private readonly IApplicationDbContext _db;

    public SetStudentPhotoCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<string?> Handle(
        SetStudentPhotoCommand request, CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var previous = student.PhotoUrl;
        student.PhotoUrl = request.PhotoUrl;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return previous;
    }
}
