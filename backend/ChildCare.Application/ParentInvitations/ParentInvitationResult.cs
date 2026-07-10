using ChildCare.Contracts.Responses;

namespace ChildCare.Application.ParentInvitations;

// NotEligible is deliberately not a member here — that check runs in
// CreateParentInvitationCommandValidator via FluentValidation (422 errors.validation), since it
// doesn't need the distinct status code the other failures below do.
public enum ParentInvitationFailure
{
    ContactNotFound,
    AlreadyHasAccount,
    OrganisationNotFound,
    InvitationInvalidOrExpired,
}

public class ParentInvitationResult
{
    public bool Succeeded { get; init; }
    public ParentInvitationFailure? Failure { get; init; }
    public ParentInvitationResponse? Response { get; init; }

    public static ParentInvitationResult Success(ParentInvitationResponse response) => new() { Succeeded = true, Response = response };
    public static ParentInvitationResult Fail(ParentInvitationFailure failure) => new() { Failure = failure };
}

public class AcceptParentInvitationResult
{
    public bool Succeeded { get; init; }
    public ParentInvitationFailure? Failure { get; init; }

    public static AcceptParentInvitationResult Success() => new() { Succeeded = true };
    public static AcceptParentInvitationResult Fail(ParentInvitationFailure failure) => new() { Failure = failure };
}
