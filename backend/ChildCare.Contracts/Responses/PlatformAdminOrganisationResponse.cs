namespace ChildCare.Contracts.Responses;

// contracts/platform-admin-portal-api.md (feature 032) — read-only directory row. ProvisioningStatus
// is surfaced as-is (research.md R6) — it is NOT an admin-controlled active/suspended toggle.
// RegisteredByEmail is the email that redeemed the invitation which created this organisation
// (research.md R5) — a historical fact, not a live "current director" lookup.
public record PlatformAdminOrganisationResponse(
    Guid Id,
    string Name,
    string Plan,
    string ProvisioningStatus,
    string? KboNumber,
    DateTime CreatedAt,
    string? RegisteredByEmail);
