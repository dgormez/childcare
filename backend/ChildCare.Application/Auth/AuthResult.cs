using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Auth;

/// <summary>
/// Shared success/failure result for every auth command that issues a session (Login,
/// RefreshToken, GoogleSignIn, AppleSignIn), mirroring RegisterOrganisationResult's shape
/// (feature 001). Mapping a failure to an HTTP status + errorKey is HTTP translation, not
/// business logic (constitution Principle III) — see contracts/auth-api.md, ERROR_KEYS.md.
/// </summary>
public class AuthResult
{
    public AuthSessionResponse? Response { get; private init; }
    public AuthFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static AuthResult Success(AuthSessionResponse response) => new() { Response = response };
    public static AuthResult Fail(AuthFailure failure) => new() { Failure = failure };
}

/// <summary>
/// Shared success/failure result for auth commands with no session payload on success
/// (VerifyEmail, ResetPassword, ForgotPassword) — reuses AuthFailure below.
/// </summary>
public class AuthActionResult
{
    public AuthFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static readonly AuthActionResult Ok = new();
    public static AuthActionResult Fail(AuthFailure failure) => new() { Failure = failure };
}

/// <summary>
/// Shared across every auth command's failure branching (research.md R9, ERROR_KEYS.md) — not
/// every command produces every value (e.g. only Google/Apple sign-in ever produce
/// MethodNotAllowedForRole), but one enum keeps endpoint-mapping code uniform.
/// </summary>
public enum AuthFailure
{
    OrganisationNotFound,
    InvalidCredentials,
    MethodNotAllowedForRole,
    TokenInvalidOrExpired,

    /// <summary>Apple's first sign-in for a given account sent no email, and the client didn't
    /// supply one either — there is nothing to match an account against (AppleSignInCommandHandler).</summary>
    AppleEmailRequiredFirstSignIn,
}
