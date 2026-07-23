using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Invitations;

internal static class PlatformAdminInvitationMapper
{
    // data-model.md's derived-status rules (research.md R2): never a stored field. `hasTenant`
    // must be resolved by the caller (a Tenant with CreatedFromInvitationId == invitation.Id) —
    // this mapper has no DB access of its own, matching every other mapper in this codebase.
    public static string DeriveStatus(Invitation invitation, bool hasTenant)
    {
        if (hasTenant) return "accepted";
        if (invitation.RevokedAt is not null) return "revoked";
        if (invitation.ExpiresAt <= DateTime.UtcNow) return "expired";
        return "pending";
    }

    public static PlatformAdminInvitationResponse ToResponse(Invitation invitation, bool hasTenant) => new(
        invitation.Id,
        invitation.Email,
        invitation.OrganisationNameNote,
        invitation.Locale,
        DeriveStatus(invitation, hasTenant),
        invitation.ExpiresAt,
        invitation.CreatedAt,
        invitation.CreatedByEmail,
        invitation.RevokedByEmail,
        invitation.RevokedAt);
}
