using MediatR;

namespace ChildCare.Application.Email;

public record UnsubscribeDigestCommand(string OrganisationSlug, string Token) : IRequest<DigestSubscriptionResult>;

public class DigestSubscriptionResult
{
    public bool Valid { get; private init; }
    public bool Unsubscribed { get; private init; }
    public string Locale { get; private init; } = "nl";

    public static DigestSubscriptionResult Ok(bool unsubscribed, string locale) => new() { Valid = true, Unsubscribed = unsubscribed, Locale = locale };
    public static DigestSubscriptionResult Invalid() => new() { Valid = false };
}

/// <summary>
/// FR-007/FR-020: sets `Contact.DigestUnsubscribedAt` if not already set — idempotent, so a
/// repeated call with the same token (double click, stale tab, bookmarked link) leaves the
/// contact unsubscribed with no error (spec.md Security considerations).
/// </summary>
public class UnsubscribeDigestCommandHandler(DigestUnsubscribeLinkResolver linkResolver) : IRequestHandler<UnsubscribeDigestCommand, DigestSubscriptionResult>
{
    public async Task<DigestSubscriptionResult> Handle(UnsubscribeDigestCommand request, CancellationToken cancellationToken)
    {
        var resolved = await linkResolver.ResolveAsync(request.OrganisationSlug, request.Token, cancellationToken);
        if (resolved is null)
            return DigestSubscriptionResult.Invalid();

        var (db, contact) = resolved.Value;
        if (contact.DigestUnsubscribedAt is null)
        {
            contact.DigestUnsubscribedAt = DateTime.UtcNow;
            contact.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return DigestSubscriptionResult.Ok(unsubscribed: true, contact.Locale);
    }
}
