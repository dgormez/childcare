using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Parent;

// Feature 030 (US5) — contracts/family-siblings-api.md, research.md R8. Mirrors
// GetParentChildrenQuery exactly but filters the opposite way (DeactivatedAt != null instead of
// == null), plus the enrollment-period dates a "previous children" view needs.
public record GetParentPreviousChildrenQuery(Guid TenantUserId) : IRequest<IReadOnlyList<ParentPreviousChildResponse>>;

public class GetParentPreviousChildrenQueryHandler(
    ITenantDbContext db,
    ICurrentParentContactResolver contactResolver,
    IProfilePhotoStorage photoStorage) : IRequestHandler<GetParentPreviousChildrenQuery, IReadOnlyList<ParentPreviousChildResponse>>
{
    public async Task<IReadOnlyList<ParentPreviousChildResponse>> Handle(GetParentPreviousChildrenQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return [];

        var children = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Join(db.Children, cc => cc.ChildId, c => c.Id, (cc, c) => c)
            .Where(c => c.DeactivatedAt != null)
            .ToListAsync(cancellationToken);

        var childIds = children.Select(c => c.Id).ToList();
        var earliestContractStartByChildId = await db.Contracts
            .Where(c => childIds.Contains(c.ChildId))
            .GroupBy(c => c.ChildId)
            .Select(g => new { ChildId = g.Key, EarliestStart = g.Min(c => c.StartDate) })
            .ToDictionaryAsync(x => x.ChildId, x => x.EarliestStart, cancellationToken);

        var results = new List<ParentPreviousChildResponse>(children.Count);
        foreach (var child in children)
        {
            var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
            DateOnly? enrollmentStart = earliestContractStartByChildId.TryGetValue(child.Id, out var start) ? start : null;
            results.Add(new ParentPreviousChildResponse(
                child.Id, child.FirstName, child.LastName, photoUrl, child.DateOfBirth,
                enrollmentStart,
                DateOnly.FromDateTime(child.DeactivatedAt!.Value)));
        }

        return results;
    }
}
