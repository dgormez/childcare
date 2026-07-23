using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Invitations;

// Found during implementation (spec.md User Story 2, AC1/AC3): the registration page needs to
// pre-fill/lock the invited email and show an invalid-link state on page load — feature 001's
// RegisterOrganisationCommand alone can't support that, since it only validates the token at
// final submission. This is a read-only, anonymous, tenant-exempt lookup, deliberately as
// narrow as RegisterOrganisationCommandHandler's own not-found/already-used checks (research.md
// R5's generic-error posture applies here too — never reveal *why* a token doesn't resolve).
public record GetInvitationInfoByTokenQuery(string Token) : IRequest<InvitationInfoResult>;

public class InvitationInfoResult
{
    public bool Succeeded { get; init; }
    public string? Email { get; init; }

    public static InvitationInfoResult Success(string email) => new() { Succeeded = true, Email = email };
    public static InvitationInfoResult Fail() => new() { Succeeded = false };
}

public class GetInvitationInfoByTokenQueryHandler(IPublicDbContext db) : IRequestHandler<GetInvitationInfoByTokenQuery, InvitationInfoResult>
{
    public async Task<InvitationInfoResult> Handle(GetInvitationInfoByTokenQuery request, CancellationToken cancellationToken)
    {
        var tokenHash = InvitationTokenCodec.HashFromPlaintext(request.Token);
        var invitation = tokenHash is null
            ? null
            : await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null || invitation.ExpiresAt <= DateTime.UtcNow || invitation.RevokedAt is not null)
            return InvitationInfoResult.Fail();

        // Mirrors RegisterOrganisationCommandHandler's ClaimOrResumeTenantAsync: only a Ready
        // tenant means "already used" — a still-provisioning one is a resumable attempt, not a
        // dead link.
        var alreadyUsed = await db.Tenants.AnyAsync(
            t => t.CreatedFromInvitationId == invitation.Id && t.ProvisioningStatus == ProvisioningStatus.Ready,
            cancellationToken);
        if (alreadyUsed)
            return InvitationInfoResult.Fail();

        return InvitationInfoResult.Success(invitation.Email);
    }
}
