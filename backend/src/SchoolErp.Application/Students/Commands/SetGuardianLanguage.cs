using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Students;
using SchoolErp.Shared.Localization;

namespace SchoolErp.Application.Students.Commands;

/// <summary>Office-side correction: sets one guardian's notification language.</summary>
public sealed record SetGuardianLanguageCommand(Guid GuardianId, string Language)
    : IRequest<string>;

/// <summary>
/// Parent-side self-service: the reader flipped the app's own EN/తెలుగు toggle,
/// so their messages should follow. Keyed by the signed-in phone rather than a
/// guardian id — one person can hold several guardian rows (siblings admitted
/// separately) and the preference belongs to the person, not the row.
/// </summary>
public sealed record SetMyNotificationLanguageCommand(string? Phone, string Language)
    : IRequest<string>;

/// <summary>Rejects languages we have no templates for — silently storing
/// "fr" would leave a parent permanently on the English fallback with the UI
/// claiming otherwise.</summary>
public sealed class SetGuardianLanguageCommandValidator
    : AbstractValidator<SetGuardianLanguageCommand>
{
    public SetGuardianLanguageCommandValidator() =>
        RuleFor(c => c.Language).Must(NotificationLanguages.IsSupported)
            .WithMessage($"Language must be one of: {string.Join(", ", NotificationLanguages.Supported)}.");
}

/// <inheritdoc cref="SetGuardianLanguageCommandValidator"/>
public sealed class SetMyNotificationLanguageCommandValidator
    : AbstractValidator<SetMyNotificationLanguageCommand>
{
    public SetMyNotificationLanguageCommandValidator() =>
        RuleFor(c => c.Language).Must(NotificationLanguages.IsSupported)
            .WithMessage($"Language must be one of: {string.Join(", ", NotificationLanguages.Supported)}.");
}

/// <summary>Sets the language on a single guardian record.</summary>
public sealed class SetGuardianLanguageCommandHandler
    : IRequestHandler<SetGuardianLanguageCommand, string>
{
    private readonly IApplicationDbContext _db;

    public SetGuardianLanguageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<string> Handle(
        SetGuardianLanguageCommand request, CancellationToken cancellationToken)
    {
        var guardian = await _db.Guardians
            .FirstOrDefaultAsync(g => g.Id == request.GuardianId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Guardian), request.GuardianId);

        guardian.PreferredLanguage = NotificationLanguages.Normalize(request.Language);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return guardian.PreferredLanguage;
    }
}

/// <summary>Sets the language on every guardian row sharing the caller's phone.</summary>
public sealed class SetMyNotificationLanguageCommandHandler
    : IRequestHandler<SetMyNotificationLanguageCommand, string>
{
    private readonly IApplicationDbContext _db;

    public SetMyNotificationLanguageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<string> Handle(
        SetMyNotificationLanguageCommand request, CancellationToken cancellationToken)
    {
        var language = NotificationLanguages.Normalize(request.Language);
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            // A signed-in user with no phone is a staff account poking a parent
            // endpoint; nothing to update, and nothing to leak either.
            return language;
        }

        var phone = request.Phone.Trim();
        var guardians = await _db.Guardians
            .Where(g => g.Phone == phone)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var guardian in guardians)
        {
            guardian.PreferredLanguage = language;
        }

        if (guardians.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return language;
    }
}
