using FluentValidation;
using MediatR;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Application.Users;

/// <summary>Thin MediatR wrappers over <see cref="IUserAdminService"/> so the
/// validation and audit pipeline applies to account administration.</summary>
public sealed record GetStaffUsersQuery(string? Search = null)
    : IRequest<IReadOnlyList<StaffUserDto>>;

/// <summary>List handler.</summary>
public sealed class GetStaffUsersQueryHandler
    : IRequestHandler<GetStaffUsersQuery, IReadOnlyList<StaffUserDto>>
{
    private readonly IUserAdminService _users;

    public GetStaffUsersQueryHandler(IUserAdminService users) => _users = users;

    public Task<IReadOnlyList<StaffUserDto>> Handle(
        GetStaffUsersQuery request, CancellationToken cancellationToken) =>
        _users.GetUsersAsync(request.Search, cancellationToken);
}

/// <summary>Creates a staff account.</summary>
public sealed record CreateStaffUserCommand(
    string FullName,
    string? Email,
    string? Phone,
    string TemporaryPassword,
    IReadOnlyList<string> Roles) : IRequest<Guid>;

/// <summary>Account shape rules; at least one contact and one role.</summary>
public sealed class CreateStaffUserCommandValidator : AbstractValidator<CreateStaffUserCommand>
{
    public CreateStaffUserCommandValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Email).EmailAddress().MaximumLength(320)
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Phone).Matches(@"^\+?[0-9]{10,15}$")
            .When(c => !string.IsNullOrWhiteSpace(c.Phone));
        RuleFor(c => c)
            .Must(c => !string.IsNullOrWhiteSpace(c.Email) || !string.IsNullOrWhiteSpace(c.Phone))
            .WithMessage("An email or a phone number is required to sign in.");
        RuleFor(c => c.TemporaryPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(c => c.Roles).NotEmpty().WithMessage("Assign at least one role.");
    }
}

/// <summary>Create handler.</summary>
public sealed class CreateStaffUserCommandHandler : IRequestHandler<CreateStaffUserCommand, Guid>
{
    private readonly IUserAdminService _users;

    public CreateStaffUserCommandHandler(IUserAdminService users) => _users = users;

    public Task<Guid> Handle(CreateStaffUserCommand request, CancellationToken cancellationToken) =>
        _users.CreateUserAsync(
            request.FullName, request.Email, request.Phone,
            request.TemporaryPassword, request.Roles, cancellationToken);
}

/// <summary>Edits a staff account (name, active flag, role set).</summary>
public sealed record UpdateStaffUserCommand(
    Guid UserId,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles) : IRequest;

/// <summary>Edit shape rules.</summary>
public sealed class UpdateStaffUserCommandValidator : AbstractValidator<UpdateStaffUserCommand>
{
    public UpdateStaffUserCommandValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Roles).NotEmpty().WithMessage("Assign at least one role.");
    }
}

/// <summary>Edit handler.</summary>
public sealed class UpdateStaffUserCommandHandler : IRequestHandler<UpdateStaffUserCommand>
{
    private readonly IUserAdminService _users;

    public UpdateStaffUserCommandHandler(IUserAdminService users) => _users = users;

    public Task Handle(UpdateStaffUserCommand request, CancellationToken cancellationToken) =>
        _users.UpdateUserAsync(
            request.UserId, request.FullName, request.IsActive, request.Roles, cancellationToken);
}

/// <summary>Admin-set temporary password for a staff account.</summary>
public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword) : IRequest;

/// <summary>Password shape rules (Identity policy still applies).</summary>
public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(c => c.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

/// <summary>Reset handler.</summary>
public sealed class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand>
{
    private readonly IUserAdminService _users;

    public ResetUserPasswordCommandHandler(IUserAdminService users) => _users = users;

    public Task Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken) =>
        _users.ResetPasswordAsync(request.UserId, request.NewPassword, cancellationToken);
}

/// <summary>Roles with their permission bundles.</summary>
public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

/// <summary>List handler.</summary>
public sealed class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly IUserAdminService _users;

    public GetRolesQueryHandler(IUserAdminService users) => _users = users;

    public Task<IReadOnlyList<RoleDto>> Handle(
        GetRolesQuery request, CancellationToken cancellationToken) =>
        _users.GetRolesAsync(cancellationToken);
}

/// <summary>Creates a role as a permission bundle.</summary>
public sealed record CreateRoleCommand(
    string Name, string? Description, IReadOnlyList<string> Permissions) : IRequest<Guid>;

/// <summary>Role shape rules; permissions must come from the catalog.</summary>
public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(256);
        RuleFor(c => c.Permissions).NotEmpty()
            .Must(p => p.All(Permissions.TenantAssignable.Contains))
            .WithMessage("Unknown permission in the set.");
    }
}

/// <summary>Create handler.</summary>
public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Guid>
{
    private readonly IUserAdminService _users;

    public CreateRoleCommandHandler(IUserAdminService users) => _users = users;

    public Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken) =>
        _users.CreateRoleAsync(
            request.Name, request.Description, request.Permissions, cancellationToken);
}

/// <summary>Replaces a role's description and permission set.</summary>
public sealed record UpdateRoleCommand(
    Guid RoleId, string? Description, IReadOnlyList<string> Permissions) : IRequest;

/// <summary>Same permission-catalog rule as creation.</summary>
public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(c => c.Description).MaximumLength(256);
        RuleFor(c => c.Permissions).NotEmpty()
            .Must(p => p.All(Permissions.TenantAssignable.Contains))
            .WithMessage("Unknown permission in the set.");
    }
}

/// <summary>Update handler.</summary>
public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand>
{
    private readonly IUserAdminService _users;

    public UpdateRoleCommandHandler(IUserAdminService users) => _users = users;

    public Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken) =>
        _users.UpdateRoleAsync(
            request.RoleId, request.Description, request.Permissions, cancellationToken);
}
