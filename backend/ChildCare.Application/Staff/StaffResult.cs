using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Staff;

/// <summary>
/// Shared success/failure result for every staff command, mirroring LocationResult (feature 004).
/// Mapping a failure to an HTTP status + errorKey is HTTP translation, not business logic
/// (constitution Principle III) — see contracts/staff-api.md, ERROR_KEYS.md. FluentValidation
/// failures never reach this type — they're handled by the shared ValidationBehavior pipeline
/// before a handler runs.
/// </summary>
public class StaffResult
{
    public StaffResponse? Response { get; private init; }
    public StaffFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static StaffResult Success(StaffResponse response) => new() { Response = response };
    public static StaffResult Fail(StaffFailure failure) => new() { Failure = failure };
}

public enum StaffFailure
{
    NotFound,
    EmailAlreadyExists,
    TenantUserNotFound,
    HasActiveDependents,
    InvitationInvalidOrExpired,
    AccountAlreadyActive,
    LocationNotFound,
    OrganisationNotFound,
}
