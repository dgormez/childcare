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

        var results = new List<ParentChildResponse>(children.Count);
        foreach (var child in children)
        {
            var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
            results.Add(new ParentChildResponse(child.Id, child.FirstName, child.LastName, photoUrl, child.DateOfBirth));
        }

        return results;
    }
}
