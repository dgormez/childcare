using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Invitations;

// contracts/platform-admin-portal-api.md — shared Result/failure shape for every platform-admin
// write operation on Invitation, mirroring PlatformAdminVaccineTypeResult's pattern (013h).
public enum PlatformAdminInvitationFailure
{
    NotFound,
    AlreadyAccepted,
}

public class PlatformAdminInvitationResult
{
    public bool Succeeded { get; init; }
    public PlatformAdminInvitationFailure? Failure { get; init; }
    public PlatformAdminInvitationResponse? Response { get; init; }

    public static PlatformAdminInvitationResult Success(PlatformAdminInvitationResponse response) => new() { Succeeded = true, Response = response };
    public static PlatformAdminInvitationResult Fail(PlatformAdminInvitationFailure failure) => new() { Failure = failure };
}
