namespace SchoolErp.AdminPortal.Models;

/// <summary>Login response envelope: either tokens or an MFA challenge.</summary>
public sealed record LoginResponseDto(
    string? AccessToken,
    int? ExpiresInSeconds,
    string? RefreshToken,
    bool? MfaRequired,
    string? MfaToken);

/// <summary>Outcome of the password step as the login page sees it.</summary>
public sealed record LoginOutcome(string? Error, string? MfaToken)
{
    public bool Succeeded => Error is null && MfaToken is null;

    public bool NeedsMfa => MfaToken is not null;
}

/// <summary>Mirrors MfaEnrollment.</summary>
public sealed record MfaEnrollmentDto(string SharedKey, string AuthenticatorUri);

/// <summary>Mirrors MfaEnableResult.</summary>
public sealed record MfaEnableResultDto(List<string> RecoveryCodes);

/// <summary>Mirrors MfaStatusResponse.</summary>
public sealed record MfaStatusDto(bool Enabled);
