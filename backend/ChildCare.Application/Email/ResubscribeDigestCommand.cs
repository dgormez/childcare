using MediatR;

namespace ChildCare.Application.Email;

public record ResubscribeDigestCommand(string OrganisationSlug, string Token) : IRequest<DigestSubscriptionResult>;

/// <summary>FR-007/FR-020: clears `Contact.DigestUnsubscribedAt` — idempotent, same shape as UnsubscribeDigestCommandHandler.</summary>
public class ResubscribeDigestCommandHandler(DigestUnsubscribeLinkResolver linkResolver) : IRequestHandler<ResubscribeDigestCommand, DigestSubscriptionResult>
{
    public async Task<DigestSubscriptionResult> Handle(ResubscribeDigestCommand request, CancellationToken cancellationToken)
    {
        var resolved = await linkResolver.ResolveAsync(request.OrganisationSlug, request.Token, cancellationToken);
        if (resolved is null)
            return DigestSubscriptionResult.Invalid();

        var (db, contact) = resolved.Value;
        if (contact.DigestUnsubscribedAt is not null)
        {
            contact.DigestUnsubscribedAt = null;
            contact.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return DigestSubscriptionResult.Ok(unsubscribed: false, contact.Locale);
    }
}
