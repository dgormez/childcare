using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ParentInvitations;

/// <summary>
/// Shared by AcceptParentInvitationCommandHandler and the Google/Apple sign-in handlers:
/// links a Contact to its TenantUser and backfills existing message-thread participation
/// (FR-006a). Extracted because a Parent-role account can complete registration two ways —
/// setting a password via the invitation-accept endpoint, or signing in with Google/Apple
/// directly (FR-000b) — and both paths must perform the exact same linking, not just the
/// password path. Before this existed, a parent who signed in via Google/Apple before ever
/// visiting the accept-invitation screen got a valid access token but no linked Contact,
/// so every ParentOnly endpoint 403'd forever (ICurrentParentContactResolver found nothing
/// to resolve) and FR-006a's thread backfill silently never ran for them.
/// </summary>
internal static class ParentAccountLinker
{
    public static async Task LinkAndBackfillThreadsAsync(
        ITenantDbContext db, Contact contact, TenantUser user, CancellationToken cancellationToken)
    {
        contact.TenantUserId = user.Id;

        var childIds = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Select(cc => cc.ChildId)
            .ToListAsync(cancellationToken);

        if (childIds.Count == 0)
            return;

        var threadIds = await db.MessageThreads
            .Where(t => t.ChildId != null && childIds.Contains(t.ChildId!.Value))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var alreadyParticipant = await db.MessageThreadParticipants
            .Where(p => p.TenantUserId == user.Id && threadIds.Contains(p.ThreadId))
            .Select(p => p.ThreadId)
            .ToListAsync(cancellationToken);

        foreach (var threadId in threadIds.Except(alreadyParticipant))
        {
            db.MessageThreadParticipants.Add(new MessageThreadParticipant
            {
                ThreadId = threadId,
                TenantUserId = user.Id,
            });
        }
    }

    /// <summary>
    /// Called from the Google/Apple sign-in handlers: if this is a Parent-role user's first
    /// successful OAuth sign-in and they haven't gone through the password accept flow yet
    /// (Contact.TenantUserId still null), find their invited Contact by email and link it —
    /// the OAuth equivalent of AcceptParentInvitationCommandHandler's password-flow linking.
    /// No-ops for every other case (already linked, or no invited Contact found), and never
    /// throws — an unresolved link here must not block a real sign-in from returning a token.
    /// </summary>
    public static async Task LinkIfUnlinkedParentAsync(ITenantDbContext db, TenantUser user, CancellationToken cancellationToken)
    {
        if (user.Role != Domain.Enums.UserRole.Parent)
            return;

        var contact = await db.Contacts.FirstOrDefaultAsync(
            c => c.TenantUserId == null && c.Email != null && c.Email.ToLower() == user.Email,
            cancellationToken);
        if (contact is null)
            return;

        await LinkAndBackfillThreadsAsync(db, contact, user, cancellationToken);
    }
}
