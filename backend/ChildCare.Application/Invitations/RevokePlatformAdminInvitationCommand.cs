using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invitations;

public record RevokePlatformAdminInvitationCommand(Guid Id, Guid ActingUserId, string ActingUserEmail)
    : IRequest<PlatformAdminInvitationResult>;

public class RevokePlatformAdminInvitationCommandHandler(IPublicDbContext publicDb)
    : IRequestHandler<RevokePlatformAdminInvitationCommand, PlatformAdminInvitationResult>
{
    public async Task<PlatformAdminInvitationResult> Handle(RevokePlatformAdminInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await publicDb.Invitations.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (invitation is null)
            return PlatformAdminInvitationResult.Fail(PlatformAdminInvitationFailure.NotFound);

        var hasTenant = await publicDb.Tenants.AnyAsync(t => t.CreatedFromInvitationId == invitation.Id, cancellationToken);
        if (hasTenant)
            return PlatformAdminInvitationResult.Fail(PlatformAdminInvitationFailure.AlreadyAccepted);

        // Idempotent no-op on an already-revoked invitation (mirrors DeactivateVaccineTypeCommand's
        // precedent) — the original revoke's who/when is preserved, not overwritten.
        if (invitation.RevokedAt is null)
        {
            invitation.RevokedByUserId = request.ActingUserId;
            invitation.RevokedByEmail = request.ActingUserEmail;
            invitation.RevokedAt = DateTime.UtcNow;
            await publicDb.SaveChangesAsync(cancellationToken);
        }

        return PlatformAdminInvitationResult.Success(PlatformAdminInvitationMapper.ToResponse(invitation, hasTenant: false));
    }
}
