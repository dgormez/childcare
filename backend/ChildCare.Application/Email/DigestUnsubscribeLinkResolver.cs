using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Email;

/// <summary>
/// Shared first step for every unsubscribe/re-subscribe/subscription-state handler (feature 020,
/// research.md R5): resolve the tenant schema from the link's `org` slug (mirrors
/// `ResetPasswordCommandHandler`'s exact shape, feature 003 — there is no JWT `tenant_id` claim
/// on these public routes), then verify the token and load the `Contact` within that schema.
/// Fails closed at every step — an invalid org, invalid/tampered token, or a token whose contact
/// no longer exists in that schema all resolve to `null`, never an exception or a leak of which
/// step failed.
/// </summary>
public class DigestUnsubscribeLinkResolver(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IUnsubscribeTokenService tokenService)
{
    public async Task<(ITenantDbContext Db, Contact Contact)?> ResolveAsync(string organisationSlug, string token, CancellationToken cancellationToken = default)
    {
        var tenant = await slugResolver.ResolveAsync(organisationSlug, cancellationToken);
        if (tenant is null)
            return null;

        var contactId = tokenService.TryParseToken(token);
        if (contactId is null)
            return null;

        var db = tenantResolver.ForSchema(tenant.SchemaName);
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == contactId.Value, cancellationToken);
        if (contact is null)
            return null;

        return (db, contact);
    }
}
