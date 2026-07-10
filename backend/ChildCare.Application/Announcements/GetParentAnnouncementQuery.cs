using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Announcements;

/// <summary>FR-009: no reply endpoint exists for announcements at all — read-only by omission.</summary>
public record GetParentAnnouncementQuery(Guid TenantUserId, Guid AnnouncementId) : IRequest<ParentAnnouncementResult>;

public class GetParentAnnouncementQueryHandler(ITenantDbContext db, ICurrentParentContactResolver contactResolver)
    : IRequestHandler<GetParentAnnouncementQuery, ParentAnnouncementResult>
{
    public async Task<ParentAnnouncementResult> Handle(GetParentAnnouncementQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return ParentAnnouncementResult.Fail(AnnouncementFailure.NotRecipient);

        var recipient = await db.AnnouncementRecipients
            .FirstOrDefaultAsync(r => r.AnnouncementId == request.AnnouncementId && r.ContactId == contact.Id, cancellationToken);
        if (recipient is null)
            return ParentAnnouncementResult.Fail(AnnouncementFailure.NotRecipient);

        var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == request.AnnouncementId, cancellationToken);
        if (announcement is null)
            return ParentAnnouncementResult.Fail(AnnouncementFailure.NotFound);

        recipient.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ParentAnnouncementResult.Success(new ParentAnnouncementResponse(
            announcement.Id, announcement.Subject, announcement.Body, announcement.SentAt, recipient.ReadAt));
    }
}
