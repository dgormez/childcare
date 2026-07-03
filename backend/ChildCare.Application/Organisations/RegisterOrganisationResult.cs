using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Organisations;

/// <summary>
/// Distinguishes outcomes that map to different HTTP status codes (contracts/register-organisation.md):
/// success → 201, InvitationNotFound → 404 (deliberately generic — research.md R5, no distinction
/// between not-found/expired/already-used), EmailMismatch → 422. Modeled as a result object rather
/// than exceptions because these are expected, common outcomes, not exceptional ones — endpoint
/// mapping of a result to a status code is HTTP translation, not business logic (constitution
/// Principle III).
/// </summary>
public class RegisterOrganisationResult
{
    public RegisterOrganisationResponse? Response { get; private init; }
    public RegisterOrganisationFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static RegisterOrganisationResult Success(RegisterOrganisationResponse response) => new() { Response = response };
    public static RegisterOrganisationResult Fail(RegisterOrganisationFailure failure) => new() { Failure = failure };
}

public enum RegisterOrganisationFailure
{
    InvitationNotFound,
    EmailMismatch,
}
