using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using ChildCare.Application.Invitations;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ParentInvitations;

/// <summary>
/// Exempt-route command (no tenant context yet — mirrors AcceptStaffInvitationCommandHandler,
/// feature 005): the organisation slug travels in the emailed link's query string, not a
/// header/JWT.
/// </summary>
public class AcceptParentInvitationCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver) : IRequestHandler<AcceptParentInvitationCommand, AcceptParentInvitationResult>
{
    public async Task<AcceptParentInvitationResult> Handle(AcceptParentInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return AcceptParentInvitationResult.Fail(ParentInvitationFailure.OrganisationNotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var tokenHash = InvitationTokenCodec.HashFromPlaintext(request.Token);
        var invitation = tokenHash is null
            ? null
            : await db.ParentInvitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null || invitation.ExpiresAt <= DateTime.UtcNow)
            return AcceptParentInvitationResult.Fail(ParentInvitationFailure.InvitationInvalidOrExpired);

        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Id == invitation.ContactId, cancellationToken);
        var user = contact is null
            ? null
            : await db.Users.FirstOrDefaultAsync(u => u.Email == invitation.Email, cancellationToken);

        if (contact is null || user is null)
            return AcceptParentInvitationResult.Fail(ParentInvitationFailure.InvitationInvalidOrExpired);

        // FR-000b: single-use enforced even before ExpiresAt elapses — a second accept attempt
        // on an already-used token fails identically to an expired one (mirrors StaffInvitation).
        if (!string.IsNullOrEmpty(user.PasswordHash))
            return AcceptParentInvitationResult.Fail(ParentInvitationFailure.InvitationInvalidOrExpired);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // FR-006a: backfill this contact onto every existing thread for their linked children,
        // with access to full prior history (not just messages sent after they joined) — shared
        // with the Google/Apple sign-in completion path (ParentAccountLinker).
        await ParentAccountLinker.LinkAndBackfillThreadsAsync(db, contact, user, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return AcceptParentInvitationResult.Success();
    }
}
