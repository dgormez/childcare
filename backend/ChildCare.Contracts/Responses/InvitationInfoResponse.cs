namespace ChildCare.Contracts.Responses;

// contracts/platform-admin-portal-api.md (feature 032) — the registration page's lookup, prior
// to submission. Deliberately minimal: just enough to pre-fill/lock the email field.
public record InvitationInfoResponse(string Email);
