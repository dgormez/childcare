namespace ChildCare.Contracts.Responses;

// contracts/platform-admin-portal-api.md (feature 032) — the platform-admin management view of
// an invitation. Deliberately excludes the raw token/tokenHash — the platform-admin never needs
// to see or copy it, only the emailed link carries it (matches CreateInvitationResponse's
// existing precedent of returning the token only to the ops-key-gated caller, not a list view).
public record PlatformAdminInvitationResponse(
    Guid Id,
    string Email,
    string? OrganisationNameNote,
    string Locale,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    string? CreatedByEmail,
    string? RevokedByEmail,
    DateTime? RevokedAt);
