using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Email;

public record GetBulkEmailRecipientCountQuery(Guid LocationId, Guid? GroupId) : IRequest<int>;

/// <summary>
/// FR-016: lets the compose screen show "0 recipients" before send is attempted, rather than a
/// submit-time surprise. Shares BulkEmailRecipientResolver's exact recipient-resolution logic
/// with SendBulkEmailCommandHandler (research.md R4 — no `TenantUserId` gate, unlike
/// SendAnnouncementCommandHandler's existing push/in-app fan-out) so the preview count and the
/// actual send always agree.
/// </summary>
public class GetBulkEmailRecipientCountQueryHandler(ITenantDbContext db) : IRequestHandler<GetBulkEmailRecipientCountQuery, int>
{
    public async Task<int> Handle(GetBulkEmailRecipientCountQuery request, CancellationToken cancellationToken)
    {
        var contacts = await BulkEmailRecipientResolver.ResolveAsync(db, request.LocationId, request.GroupId, cancellationToken);
        return contacts.Count;
    }
}

/// <summary>
/// Shared recipient resolution for bulk email (feature 020). Deliberately does NOT filter on
/// `Contact.TenantUserId != null` — unlike `SendAnnouncementCommandHandler`'s existing push/
/// in-app fan-out (FR-008 there), email is reachable by any contact with an address on file,
/// independent of whether they ever accepted a parent-app invitation (research.md R4). Filters
/// on `Contact.Email != null` here (not in the caller) so the recipient-count preview and the
/// actual send never disagree about who's "in scope" — SendBulkEmailCommandHandler additionally
/// records a `SkippedNoEmail` outcome per excluded contact for the delivery-outcome summary.
/// </summary>
internal static class BulkEmailRecipientResolver
{
    public static async Task<List<Domain.Entities.Contact>> ResolveAsync(
        ITenantDbContext db, Guid locationId, Guid? groupId, CancellationToken cancellationToken)
    {
        var childIds = await ResolveChildIdsAsync(db, locationId, groupId, cancellationToken);
        if (childIds.Count == 0)
            return [];

        return await db.ChildContacts
            .Where(cc => childIds.Contains(cc.ChildId))
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .Where(c => c.Email != null)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>Every contact linked to a child in scope, with or without an email on file —
    /// used by SendBulkEmailCommandHandler to also identify (and log) skipped-no-email contacts.</summary>
    public static async Task<List<Domain.Entities.Contact>> ResolveAllContactsAsync(
        ITenantDbContext db, Guid locationId, Guid? groupId, CancellationToken cancellationToken)
    {
        var childIds = await ResolveChildIdsAsync(db, locationId, groupId, cancellationToken);
        if (childIds.Count == 0)
            return [];

        return await db.ChildContacts
            .Where(cc => childIds.Contains(cc.ChildId))
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<Guid>> ResolveChildIdsAsync(
        ITenantDbContext db, Guid locationId, Guid? groupId, CancellationToken cancellationToken)
    {
        var recipientQuery = db.ChildGroupAssignments
            .Where(a => a.EndDate == null)
            .Join(db.Children, a => a.ChildId, c => c.Id, (a, c) => new { a.ChildId, a.GroupId, c.DeactivatedAt });

        if (groupId is Guid group)
        {
            return await recipientQuery
                .Where(x => x.GroupId == group && x.DeactivatedAt == null)
                .Select(x => x.ChildId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        var locationGroupIds = await db.Groups.Where(g => g.LocationId == locationId).Select(g => g.Id).ToListAsync(cancellationToken);
        return await recipientQuery
            .Where(x => locationGroupIds.Contains(x.GroupId) && x.DeactivatedAt == null)
            .Select(x => x.ChildId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
