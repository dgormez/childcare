using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invitations;

public class CreateInvitationCommandHandler(IPublicDbContext db) : IRequestHandler<CreateInvitationCommand, CreateInvitationResponse>
{
    // No expiry duration is specified anywhere in spec.md/research.md — 7 days is a reasonable
    // default for an operator-issued, early-access invitation; revisit if that assumption changes.
    private static readonly TimeSpan InvitationValidity = TimeSpan.FromDays(7);

    public async Task<CreateInvitationResponse> Handle(CreateInvitationCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        // Email has no DB-level uniqueness constraint (an operator may legitimately re-invite
        // the same address more than once — e.g. the first email never arrived). Superseding
        // any still-valid prior invitation here means there's never more than one *usable*
        // invitation per email at a time, without blocking re-invitation outright.
        var stillPending = await db.Invitations
            .Where(i => i.Email == email && i.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var pending in stillPending)
            pending.ExpiresAt = now;

        var (token, tokenHash) = InvitationTokenCodec.Generate();
        var expiresAt = now.Add(InvitationValidity);

        var invitation = new Invitation
        {
            Email = email,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateInvitationResponse(invitation.Id, invitation.Email, token, expiresAt);
    }
}
