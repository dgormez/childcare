using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

public enum TourResponseOutcome
{
    /// <summary>Bad org slug, unparseable/tampered token, or no matching entry — fails closed,
    /// no distinction surfaced to the caller (mirrors DigestUnsubscribeLinkResolver).</summary>
    Invalid,

    /// <summary>The entry already reached a terminal status (Enrolled/Withdrawn) — FR-018, the
    /// response is not recorded.</summary>
    NoLongerActive,

    Recorded,
}

public record RespondTourInvitationResult(TourResponseOutcome Outcome, string Locale, string? ChildName, bool Accepted);

/// <summary>
/// FR-016/FR-018. Anonymous, tenant-exempt — resolves its own tenant schema from
/// <paramref name="OrganisationSlug"/>, exactly like `DigestUnsubscribeLinkResolver`. Idempotent:
/// a repeated click with the same token/response is a no-op that still reports `Recorded`.
/// </summary>
public record RespondTourInvitationCommand(string OrganisationSlug, string Token, string Response) : IRequest<RespondTourInvitationResult>;

public class RespondTourInvitationCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    ITourInvitationTokenService tokenService) : IRequestHandler<RespondTourInvitationCommand, RespondTourInvitationResult>
{
    public async Task<RespondTourInvitationResult> Handle(RespondTourInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return new RespondTourInvitationResult(TourResponseOutcome.Invalid, "nl", null, false);

        var entryId = tokenService.TryParseToken(request.Token);
        if (entryId is null)
            return new RespondTourInvitationResult(TourResponseOutcome.Invalid, "nl", null, false);

        var db = tenantResolver.ForSchema(tenant.SchemaName);
        var entry = await db.WaitingListEntries.FirstOrDefaultAsync(e => e.Id == entryId.Value, cancellationToken);
        if (entry is null)
            return new RespondTourInvitationResult(TourResponseOutcome.Invalid, "nl", null, false);

        var locale = entry.SubmittedLocale ?? "nl";
        var childName = $"{entry.ChildFirstName} {entry.ChildLastName}";

        // FR-018: a response arriving after the entry reached a terminal status never reopens
        // or alters it.
        if (entry.Status is WaitingListStatus.Enrolled or WaitingListStatus.Withdrawn)
            return new RespondTourInvitationResult(TourResponseOutcome.NoLongerActive, locale, childName, false);

        var accepted = request.Response.Equals("accepted", StringComparison.OrdinalIgnoreCase);
        entry.TourInvitationStatus = accepted ? TourInvitationStatus.Accepted : TourInvitationStatus.Declined;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new RespondTourInvitationResult(TourResponseOutcome.Recorded, locale, childName, accepted);
    }
}
