using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Parent;

public record GetParentChildrenQuery(Guid TenantUserId) : IRequest<IReadOnlyList<ParentChildResponse>>;

public class GetParentChildrenQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IProfilePhotoStorage photoStorage) : IRequestHandler<GetParentChildrenQuery, IReadOnlyList<ParentChildResponse>>
{
    public async Task<IReadOnlyList<ParentChildResponse>> Handle(GetParentChildrenQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return [];

        var children = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Join(db.Children, cc => cc.ChildId, c => c.Id, (cc, c) => c)
            .Where(c => c.DeactivatedAt == null)
            .ToListAsync(cancellationToken);

        var childIds = children.Select(c => c.Id).ToList();
        // Feature 021 — same "any active contract at a QR-enabled location" gate
        // IssueCheckInCodeCommandHandler applies, computed once for every linked child rather
        // than per-child N+1 queries.
        var qrEnabledChildIds = (await db.Contracts
            .Where(c => childIds.Contains(c.ChildId) && c.Status == Domain.Enums.ContractStatus.Active)
            .Join(db.Locations, c => c.LocationId, l => l.Id, (c, l) => new { c.ChildId, l.QrCheckInEnabled })
            .Where(x => x.QrCheckInEnabled)
            .Select(x => x.ChildId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var results = new List<ParentChildResponse>(children.Count);
        foreach (var child in children)
        {
            var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
            results.Add(new ParentChildResponse(
                child.Id, child.FirstName, child.LastName, photoUrl, child.DateOfBirth, qrEnabledChildIds.Contains(child.Id)));
        }

        return results;
    }
}
