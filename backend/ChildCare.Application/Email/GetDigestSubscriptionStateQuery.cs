using MediatR;

namespace ChildCare.Application.Email;

public record GetDigestSubscriptionStateQuery(string OrganisationSlug, string Token) : IRequest<DigestSubscriptionResult>;

/// <summary>Read-only lookup backing the GET unsubscribe page's current-state display (contracts/email-communications-api.md).</summary>
public class GetDigestSubscriptionStateQueryHandler(DigestUnsubscribeLinkResolver linkResolver) : IRequestHandler<GetDigestSubscriptionStateQuery, DigestSubscriptionResult>
{
    public async Task<DigestSubscriptionResult> Handle(GetDigestSubscriptionStateQuery request, CancellationToken cancellationToken)
    {
        var resolved = await linkResolver.ResolveAsync(request.OrganisationSlug, request.Token, cancellationToken);
        return resolved is null
            ? DigestSubscriptionResult.Invalid()
            : DigestSubscriptionResult.Ok(unsubscribed: resolved.Value.Contact.DigestUnsubscribedAt is not null, resolved.Value.Contact.Locale);
    }
}
