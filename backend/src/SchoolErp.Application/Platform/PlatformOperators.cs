using FluentValidation;
using MediatR;

namespace SchoolErp.Application.Platform;

/// <summary>One platform operator (a Super Admin account, no school).</summary>
public sealed record PlatformOperatorDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    bool IsActive,
    bool MfaEnabled,
    DateTimeOffset CreatedAt);

/// <summary>
/// Managing the people who can administer every school. Implemented in
/// Infrastructure over ASP.NET Identity, alongside the tenant-scoped
/// <c>IUserAdminService</c> but deliberately separate: these accounts have no
/// tenant, and mixing them into the school-scoped service is how one would
/// eventually leak into a school's user list.
/// </summary>
public interface IPlatformOperatorService
{
    Task<IReadOnlyList<PlatformOperatorDto>> GetOperatorsAsync(CancellationToken ct = default);

    /// <summary>Creates an operator with a temporary password. They must turn
    /// MFA on before the platform screens will let them do anything.</summary>
    Task<Guid> CreateOperatorAsync(
        string fullName, string email, string temporaryPassword, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables an operator. Disabling revokes their sessions.
    /// Refuses to disable the caller, and refuses to leave the platform with
    /// no active operator at all.
    /// </summary>
    Task SetOperatorActiveAsync(Guid operatorId, bool isActive, CancellationToken ct = default);

    /// <summary>Admin-set temporary password; revokes their open sessions.</summary>
    Task ResetOperatorPasswordAsync(
        Guid operatorId, string newPassword, CancellationToken ct = default);
}

/// <summary>Operators of the platform, newest first.</summary>
public sealed record GetPlatformOperatorsQuery : IRequest<IReadOnlyList<PlatformOperatorDto>>;

/// <summary>Adds an operator. Audited like every other command.</summary>
public sealed record CreatePlatformOperatorCommand(
    string FullName, string Email, string TemporaryPassword) : IRequest<Guid>;

/// <summary>Enables or disables an operator.</summary>
public sealed record SetPlatformOperatorActiveCommand(Guid OperatorId, bool IsActive)
    : IRequest;

/// <summary>Sets an operator's password.</summary>
public sealed record ResetPlatformOperatorPasswordCommand(Guid OperatorId, string NewPassword)
    : IRequest;

/// <summary>An operator signs in by email; a phone is not enough to be one.</summary>
public sealed class CreatePlatformOperatorCommandValidator
    : AbstractValidator<CreatePlatformOperatorCommand>
{
    public CreatePlatformOperatorCommandValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(c => c.TemporaryPassword).NotEmpty().MinimumLength(12)
            .WithMessage("A platform password must be at least 12 characters.");
    }
}

/// <inheritdoc cref="CreatePlatformOperatorCommandValidator"/>
public sealed class ResetPlatformOperatorPasswordCommandValidator
    : AbstractValidator<ResetPlatformOperatorPasswordCommand>
{
    public ResetPlatformOperatorPasswordCommandValidator() =>
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(12)
            .WithMessage("A platform password must be at least 12 characters.");
}

/// <summary>Thin handlers over <see cref="IPlatformOperatorService"/>.</summary>
public sealed class PlatformOperatorHandlers :
    IRequestHandler<GetPlatformOperatorsQuery, IReadOnlyList<PlatformOperatorDto>>,
    IRequestHandler<CreatePlatformOperatorCommand, Guid>,
    IRequestHandler<SetPlatformOperatorActiveCommand>,
    IRequestHandler<ResetPlatformOperatorPasswordCommand>
{
    private readonly IPlatformOperatorService _operators;

    public PlatformOperatorHandlers(IPlatformOperatorService operators) =>
        _operators = operators;

    public Task<IReadOnlyList<PlatformOperatorDto>> Handle(
        GetPlatformOperatorsQuery request, CancellationToken cancellationToken) =>
        _operators.GetOperatorsAsync(cancellationToken);

    public Task<Guid> Handle(
        CreatePlatformOperatorCommand request, CancellationToken cancellationToken) =>
        _operators.CreateOperatorAsync(
            request.FullName, request.Email, request.TemporaryPassword, cancellationToken);

    public Task Handle(
        SetPlatformOperatorActiveCommand request, CancellationToken cancellationToken) =>
        _operators.SetOperatorActiveAsync(
            request.OperatorId, request.IsActive, cancellationToken);

    public Task Handle(
        ResetPlatformOperatorPasswordCommand request, CancellationToken cancellationToken) =>
        _operators.ResetOperatorPasswordAsync(
            request.OperatorId, request.NewPassword, cancellationToken);
}
