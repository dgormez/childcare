using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Invitations;

// FR-005: resend is allowed for anything not yet Accepted (Pending, Expired, or even an
// already-Revoked row — functionally identical to creating a fresh invitation for that email,
// FR-007 only forbids acting on Accepted). Mirrors ResendStaffInvitationCommand's shape.
public record ResendPlatformAdminInvitationCommand(Guid Id, Guid ActingUserId, string ActingUserEmail)
    : IRequest<PlatformAdminInvitationResult>;

public class ResendPlatformAdminInvitationCommandHandler(IPublicDbContext publicDb, IEmailSender emailSender, IConfiguration config)
    : IRequestHandler<ResendPlatformAdminInvitationCommand, PlatformAdminInvitationResult>
{
    private static readonly TimeSpan InvitationValidity = TimeSpan.FromDays(7);

    public async Task<PlatformAdminInvitationResult> Handle(ResendPlatformAdminInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await publicDb.Invitations.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (invitation is null)
            return PlatformAdminInvitationResult.Fail(PlatformAdminInvitationFailure.NotFound);

        var hasTenant = await publicDb.Tenants.AnyAsync(t => t.CreatedFromInvitationId == invitation.Id, cancellationToken);
        if (hasTenant)
            return PlatformAdminInvitationResult.Fail(PlatformAdminInvitationFailure.AlreadyAccepted);

        var now = DateTime.UtcNow;

        // FR-008/research.md R12: the resent-from row and every other still-usable row for this
        // email are superseded, attributed to the platform-admin performing this action — same
        // supersede path CreatePlatformAdminInvitationCommand uses for a duplicate-email create.
        await CreatePlatformAdminInvitationCommandHandler.SupersedeExistingAsync(
            publicDb, invitation.Email, request.ActingUserId, request.ActingUserEmail, now, cancellationToken);

        var (token, tokenHash) = InvitationTokenCodec.Generate();
        var fresh = new Invitation
        {
            Email = invitation.Email,
            TokenHash = tokenHash,
            ExpiresAt = now.Add(InvitationValidity),
            OrganisationNameNote = invitation.OrganisationNameNote,
            Locale = invitation.Locale,
            CreatedByUserId = request.ActingUserId,
            CreatedByEmail = request.ActingUserEmail,
        };

        publicDb.Invitations.Add(fresh);
        await publicDb.SaveChangesAsync(cancellationToken);

        var registerUrl = OrganisationInvitationLinkBuilder.BuildRegisterUrl(config, token);
        await emailSender.SendOrganisationInvitationAsync(fresh.Email, fresh.Locale, fresh.OrganisationNameNote, registerUrl, cancellationToken);

        return PlatformAdminInvitationResult.Success(PlatformAdminInvitationMapper.ToResponse(fresh, hasTenant: false));
    }
}
