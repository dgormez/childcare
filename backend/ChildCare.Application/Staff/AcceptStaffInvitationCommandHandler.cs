using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using ChildCare.Application.Invitations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

/// <summary>
/// Exempt-route command (no tenant context yet — found during implementation, mirrors
/// ResetPasswordCommandHandler, feature 003): the organisation slug travels in the emailed
/// link's query string, not a header/JWT.
/// </summary>
public class AcceptStaffInvitationCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver) : IRequestHandler<AcceptStaffInvitationCommand, StaffResult>
{
    public async Task<StaffResult> Handle(AcceptStaffInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return StaffResult.Fail(StaffFailure.OrganisationNotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var tokenHash = InvitationTokenCodec.HashFromPlaintext(request.Token);
        var invitation = tokenHash is null
            ? null
            : await db.StaffInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null || invitation.ExpiresAt <= DateTime.UtcNow)
            return StaffResult.Fail(StaffFailure.InvitationInvalidOrExpired);

        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.Id == invitation.StaffProfileId, cancellationToken);
        var user = profile is null
            ? null
            : await db.Users.FirstOrDefaultAsync(u => u.Id == profile.TenantUserId, cancellationToken);

        if (profile is null || user is null)
            return StaffResult.Fail(StaffFailure.InvitationInvalidOrExpired);

        // FR-006b: single-use enforced even before ExpiresAt elapses — a second accept attempt
        // on an already-used token fails identically to an expired one (research.md R9).
        if (!string.IsNullOrEmpty(user.PasswordHash))
            return StaffResult.Fail(StaffFailure.InvitationInvalidOrExpired);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        await db.SaveChangesAsync(cancellationToken);

        return StaffResult.Success(StaffMapper.ToResponse(profile, user, [], null));
    }
}
